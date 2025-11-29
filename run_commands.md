# ML-Agents Training Commands

## Complete Workflow (Start to Finish)

### 1. Activate Virtual Environment
First, activate the mlagents-env virtual environment:

```bash
mlagents-env\Scripts\activate
```

You should see `(mlagents-env)` appear in your terminal prompt.

### 2. Start Training

**New Training Run (overwrites existing):**
```bash
mlagents-learn chase_config.yaml --run-id=ChaseTest1 --force
```

**Resume Previous Training:**
```bash
mlagents-learn chase_config.yaml --run-id=ChaseTest1 --resume
```

**New Training with Different Run ID:**
```bash
mlagents-learn chase_config.yaml --run-id=NewChaseTest1 --force
```

### 3. Press Play in Unity
Once you see "Start training by pressing the Play button in the Unity Editor", switch to Unity and press Play.

### 4. Monitor Training
Watch the console output for training progress. Training stats are shown periodically.

### 5. Stop Training
- Press `Ctrl + C` in the terminal when satisfied with training progress
- Or let it complete based on your config's max_steps

### 6. Export to ONNX (Optional)
Export the trained model to ONNX format for use in Unity builds:
```bash
python -m mlagents.trainers.learn_to_onnx --model-path results/ChaseTest1 --output-path models/ChaseTest1.onnx
```

### 7. Deactivate Virtual Environment (When Done)
```bash
deactivate
```

---

## Quick Reference

### View Training Progress with TensorBoard
```bash
tensorboard --logdir results/
```
Then open http://localhost:6006 in your browser.

### Common Parameters
- `--run-id`: Unique identifier for this training run
- `--force`: Overwrite existing run with the same ID
- `--resume`: Resume training from a previous checkpoint
- `--env`: Path to Unity executable (for standalone builds)
- `--num-envs`: Number of parallel training environments
- `--no-graphics`: Run Unity in headless mode (faster training)

### File Locations
- **Config file**: `chase_config.yaml`
- **Results**: `results/[run-id]/`
- **ONNX models**: `models/` (or wherever you export them)
- **Unity models**: Place `.onnx` files in `AdaptiveBossAI_Prototype/Assets/` for use in Unity
