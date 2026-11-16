# Stage 3 verification

Unity 6000.6.7f1, 2026-11-15. 147 EditMode and 7 PlayMode tests passed.

1,008 puzzles generated and replayed: six profiles, three exit modes, 56 seeds per combination. Zero failed recipes and zero solution validation failures. CPU generation measurements use the Unity batch test environment and include construction, quality checks, and verification.

| Profile | Mode | Puzzles | Mean ms | Mean paths | Mean coverage | Minimum coverage | Minimum cross-face paths |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Tutorial | EdgeOnly | 56 | 1.8 | 24 | 69.1% | 68.1% | 11 |
| Tutorial | GateOnly | 56 | 1.8 | 25 | 69.3% | 68.1% | 13 |
| Tutorial | Hybrid | 56 | 1.9 | 25 | 69.2% | 68.1% | 13 |
| Easy | EdgeOnly | 56 | 3.4 | 44 | 75.6% | 75.0% | 16 |
| Easy | GateOnly | 56 | 3.9 | 44 | 75.5% | 75.0% | 18 |
| Easy | Hybrid | 56 | 3.7 | 43 | 75.6% | 75.0% | 17 |
| Medium | EdgeOnly | 56 | 6.9 | 61 | 81.3% | 81.0% | 21 |
| Medium | GateOnly | 56 | 5.7 | 63 | 81.3% | 81.0% | 22 |
| Medium | Hybrid | 56 | 4.8 | 64 | 81.3% | 81.0% | 25 |
| Hard | EdgeOnly | 56 | 8.4 | 105 | 86.4% | 86.1% | 29 |
| Hard | GateOnly | 56 | 8.8 | 92 | 86.1% | 81.5% | 34 |
| Hard | Hybrid | 56 | 7.4 | 116 | 86.2% | 86.1% | 37 |
| Very Hard | EdgeOnly | 56 | 16.5 | 157 | 86.9% | 82.4% | 39 |
| Very Hard | GateOnly | 56 | 15.5 | 131 | 84.0% | 80.1% | 42 |
| Very Hard | Hybrid | 56 | 15.0 | 189 | 90.1% | 89.7% | 44 |
| Expert | EdgeOnly | 56 | 25.1 | 208 | 83.5% | 80.0% | 51 |
| Expert | GateOnly | 56 | 30.2 | 175 | 81.7% | 80.1% | 50 |
| Expert | Hybrid | 56 | 23.6 | 255 | 89.9% | 87.6% | 56 |
