using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Threading.Tasks;
#endif
using ArrowCube.Generation;

namespace ArrowCube.Presentation
{
    /// <summary>Desktop worker or cooperative browser work, with identical recipes and
    /// validation. Web players never schedule managed work on the thread pool.</summary>
    public sealed class GenerationOperation : IDisposable
    {
        private readonly CancellationTokenSource cancellation=new CancellationTokenSource();
        private readonly ProgressRelay progress=new ProgressRelay();
        private IEnumerator<GeneratedPuzzle> steps;
#if !UNITY_WEBGL || UNITY_EDITOR
        private Task<GeneratedPuzzle> worker;
#endif
        private bool disposed;
        public static bool RequiresMainThread
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }
        public bool UsesMainThread { get; }
        public bool IsCompleted { get; private set; }
        public bool IsCanceled { get; private set; }
        public GeneratedPuzzle Result { get; private set; }
        public Exception Error { get; private set; }
        public GenerationProgress Progress => progress.Latest;
        public GenerationOperation(PuzzleRecipe recipe,bool useMainThread=false)
        {
            if(recipe==null) throw new ArgumentNullException(nameof(recipe));
            UsesMainThread=RequiresMainThread || useMainThread;
            var token=cancellation.Token;
            if(UsesMainThread) steps=new PuzzleGenerator().GenerateSteps(recipe,progress,token).GetEnumerator();
#if !UNITY_WEBGL || UNITY_EDITOR
            else worker=Task.Run(()=>new PuzzleGenerator().Generate(recipe,progress,token),token);
#endif
        }
        public void Tick(double budgetMilliseconds=6)
        {
            if(budgetMilliseconds<0 || double.IsNaN(budgetMilliseconds) || double.IsInfinity(budgetMilliseconds)) throw new ArgumentOutOfRangeException(nameof(budgetMilliseconds));
            if(disposed || IsCompleted) return;
#if !UNITY_WEBGL || UNITY_EDITOR
            if(worker!=null)
            {
                if(!worker.IsCompleted) return;
                IsCanceled=worker.IsCanceled;
                if(worker.IsFaulted) Error=worker.Exception.GetBaseException();
                else if(!IsCanceled) Result=worker.Result;
                IsCompleted=true; return;
            }
#endif
            var watch=Stopwatch.StartNew();
            try
            {
                do
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    if(!steps.MoveNext()) throw new InvalidOperationException("Generation ended without a validated puzzle.");
                    if(steps.Current!=null)
                    {
                        Result=steps.Current; IsCompleted=true; steps.Dispose(); steps=null; return;
                    }
                } while(watch.Elapsed.TotalMilliseconds<budgetMilliseconds);
            }
            catch(OperationCanceledException) { IsCanceled=true; Finish(); }
            catch(Exception error) { Error=error; Finish(); }
        }
        private void Finish() { IsCompleted=true; steps?.Dispose(); steps=null; }
        public void Cancel()
        {
            if(disposed || IsCompleted) return;
            cancellation.Cancel(); IsCanceled=true; Finish();
        }
        public void Dispose()
        {
            if(disposed) return;
            Cancel(); disposed=true;
#if !UNITY_WEBGL || UNITY_EDITOR
            if(worker!=null) worker.ContinueWith(job=> { var ignored=job.Exception; },TaskContinuationOptions.OnlyOnFaulted);
#endif
            steps?.Dispose(); steps=null; cancellation.Dispose();
        }
        private sealed class ProgressRelay : IProgress<GenerationProgress>
        {
            public volatile GenerationProgress Latest;
            public void Report(GenerationProgress value) => Latest=value;
        }
    }
}
