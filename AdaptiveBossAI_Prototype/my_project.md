

# 📘 **Reinforcement Learning POC — Detailed Technical Specification (For AI Agent)**

## **Project Title**

**Two-Ball Multi-Agent Reinforcement Learning Demo (Chaser vs Evader)**
**Platform:** Unity + ML-Agents (PPO)
**Target Date:** November 2025

---

# 🎯 **Purpose of the POC**

To implement a minimal but complete reinforcement learning demonstration showing:

* Multi-agent interaction
* Opposing goals (pursuit vs evasion)
* PPO-based training
* Real-time visualization
* Agents adapting to each other over a short session

This should be simple, clean, and reliable enough to demo live on a standard laptop.

---

# 🧩 **Core Concept**

Two spheres (“balls”) interact in a 2D environment:

### **Agent 1: Chaser**

* Learns to *pursue* the Evader
* Rewarded for reducing distance; heavily rewarded for catching
* Penalized for time wasted or moving away

### **Agent 2: Evader**

* Learns to *avoid* the Chaser
* Rewarded for increasing distance; rewarded for surviving
* Penalized for being caught

The environment resets after a catch or after a maximum episode length (≈ 5 minutes).

---

# 🏗️ **System Architecture Overview**

## **1. Unity Environment**

A lightweight 2D scene containing:

* A bounded square arena
* Two spherical GameObjects:

  * `Chaser`
  * `Evader`
* Camera + possible UI overlay

Physics:

* Unity **2D physics** (`Rigidbody2D`, `Collider2D`)
* Minimal collision layers
* Low computational load to maintain ~60 FPS

Scene characteristics:

* No heavy shaders
* Single camera
* Simple background

---

## **2. ML-Agents Integration**

Each ball is an **Agent** with:

* **Behavior Parameters** (vector observations, vector/discrete actions)
* **DecisionRequester** (e.g., 5–10 decisions per second)
* A custom C# script implementing:

  * `CollectObservations`
  * `OnActionReceived`
  * `Heuristic` (optional)
  * `OnEpisodeBegin`

### **Observations (example)**

Each agent observes:

* Its own position (x,y)
* Opponent’s position (x,y)
* Distance vector (dx, dy)
* Its own velocity
* Opponent’s velocity

### **Actions**

Two options:

1. **Discrete** (e.g., 4 directions: up/down/left/right)
2. **Continuous** (preferred): 2D vector force (x,y)

---

## **3. Rewards**

### **Chaser Rewards**

| Condition           | Reward             |
| ------------------- | ------------------ |
| Catching Evader     | +1.0               |
| Reducing distance   | +0.01 per timestep |
| Increasing distance | -0.01              |
| Time penalty        | -0.0005 per step   |

### **Evader Rewards**

| Condition               | Reward |
| ----------------------- | ------ |
| Surviving until timeout | +1.0   |
| Increasing distance     | +0.01  |
| Distance decreases      | -0.01  |
| Getting caught          | -1.0   |

---

## **4. Training System**

### **Algorithm**

* **PPO** (Proximal Policy Optimization)
* Using ML-Agents Python trainer

### **Trainer Configuration**

A YAML config defines:

* Batch size
* Buffer size
* Learning rate
* Beta/epsilon for PPO clipping
* Entropy regularization
* Curriculum options
* Self-play (optional)

### **Training Invocation**

```bash
mlagents-learn config.yaml --run-id=ChaserEvaderRun
```

Then press **Play** in Unity to connect environment training.

---

## **5. Real-Time Visualization**

The POC must show learning evolution during training.

Options:

* TensorBoard reward curves
* Unity UI panel showing:

  * Current episode reward
  * Distance between agents
  * Number of successful catches/escapes
* Optional heatmap of chaser/evader movement

Visualization is essential for live demonstration.

---

# 🧪 **Performance Requirements**

### **Minimum target**

* **Maintain ~60 FPS** in Unity during inference
* Training + visualization must not drop FPS significantly
* Scene must be extremely lightweight (2D collision only)

### **Training Behavior**

* Agents should demonstrate **visible adaptation** within a **5-minute window**:

  * Chaser improves at intercept patterns
  * Evader learns curved escape paths, not random motion

---

# 📦 **Deliverables**

## ✔️ **1. Unity Project (Working Scene)**

Contains:

* `Scenes/ChaserEvader.unity`
* Chaser and Evader GameObjects with ML-Agents components
* Prefabs for both balls
* Reward & observation logic implemented in C#

## ✔️ **2. Agent Scripts**

Two C# scripts:

* `ChaserAgent.cs`
* `EvaderAgent.cs`
  Must include:
* Observations
* Actions
* Rewards
* Episode resets
* Debug gizmos (optional)

## ✔️ **3. PPO Trainer Config**

`config.yaml` with:

* Behavior definitions for Chaser and Evader
* PPO hyperparameters
* Optional self-play and curriculum

## ✔️ **4. Training Workflow**

Clear instructions for:

* Running `mlagents-learn`
* Connecting Unity environment
* Viewing TensorBoard logs

## ✔️ **5. Demo-ready Visualization**

Real-time UI elements or TensorBoard dashboard
FPS maintained near 60 during inference
Clear demonstration of learning progress

---

# 🛠️ **Optional Enhancements (If Time Allows)**

### **Curriculum Learning**

* Slowly shrink or enlarge arena
* Increase speed caps over time

### **Self-Play / Adversarial Training**

Evader improves as chaser improves

### **Auto-scene Generator**

Editor script to generate entire arena with one button click

---

# 🤖 **AI Agent Instructions**

This project requires the AI agent to:

1. **Create and/or modify Unity C# scripts**
2. **Configure ML-Agents behaviors and PPO YAML**
3. **Assist in scene setup via scripting or detailed instructions**
4. **Ensure environment maintains performance goals**
5. **Document all steps**
6. **Provide explanations for any design decision if asked**
7. **Adapt reward functions or training settings if problems arise**
8. **Optionally generate Unity Editor tools to automate scene creation**

The AI agent should assume it has to produce:

* Working code
* Reliable instructions
* Debugging support
* Optimization guidance

---

# 🧭 **Success Criteria**

The POC is successful if:

### ✔️ Two agents learn usable policies

### ✔️ Chaser demonstrates non-random pursuit

### ✔️ Evader demonstrates avoidance behavior

### ✔️ Results appear clearly within ~5 minutes

### ✔️ Runs smoothly on a normal laptop (~60 FPS)

### ✔️ Trainer + scene code is clean and reusable

### ✔️ Demonstration is presentable to an instructor or team
