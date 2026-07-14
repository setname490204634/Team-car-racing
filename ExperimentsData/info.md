# Experiment Logs

The experiments referenced in the thesis are referred to here as **tests**. For example:

- **Experiment 1** → `test1`

## Viewing the Logs

The training logs are provided as ZIP archives. Extract the desired archive and open the logs with TensorBoard. For example:

```bash
tensorboard --logdir=pythonSide/env_logs/env_1 --host=127.0.0.1 --port=6006
```

Then open the displayed local URL (typically `http://127.0.0.1:6006`) in your browser.

## Notes

- **Test 2 – Run 3** was trained in two stages. The second stage resumed training from a checkpoint created during the first stage.

- The TensorBoard logs may contain more training episodes than the final checkpoint. This is because checkpoints were saved periodically rather than after every iteration. For example, if the latest checkpoint corresponds to iteration 150, training may have continued until iteration 199 before being stopped. In this case, the logs contain data up to iteration 199, while the latest available checkpoint remains the one from iteration 150.

- Each experiment directory also includes copies of the files that were used for that particular experiment.

- Due to git file size limits some experiments are not zipped together.