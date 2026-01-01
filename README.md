# Gestalt Engine Unlimited / 格式塔枢纽无限升级

[English](#english) | [简体中文](#简体中文)

---

## English

### Description
A RimWorld mod that removes the level 4 cap of the Gestalt Engine, allowing theoretically unlimited upgrades with customizable growth curves.

### New BVR Extension Features
- **Remote Signal Communication**:
  - **Gestalt Signal Relay**: New building that extends the Gestalt network's range significantly.
  - **Cross-Map Control**: Mechanoids can now operate on world maps and other settlements if within signal range.
  - **Remote Hacking**: Hack mechanoids on remote maps from the comfort of your base.
  - **Caravan Support**: Gestalt-controlled mechanoids can now board shuttles and join caravans without "Level Too Low" errors.

### Features
- **Unlimited Upgrades**: Break through the original 4-level limitation
- **Dual Curve Modes**:
  - **Linear Mode**: Fixed slope continuous growth, same increment each level
  - **Logarithmic Mode** (Recommended): Convergent curve with better game balance
- **Fully Customizable Parameters**:
  - Power consumption growth
  - Mech bandwidth growth
  - Control group growth
  - Research speed growth
  - Heat production growth
  - Upgrade cooldown & duration
  - **NEW**: Gestalt Signal Relay power consumption (adjustable 0-50,000 W)
- **Smart Caretaker Safety**: Prevents Caretakers from self-destructing when inside shuttles/pods.
- **Developer Tools**: Instant cooldown button (Dev Mode only), Reset Hack Cooldown.
- **Full Localization**: English & Simplified Chinese

### Requirements
- RimWorld 1.6
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)
- [Vanilla Expanded Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=1854607105)
- [Reinforced Mechanoid 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3541404610)

### Installation
1. Subscribe on Steam Workshop (link coming soon)
2. Or manually download and extract to `RimWorld/Mods/` folder
3. Enable in mod list (load after dependencies)

### Configuration
Access mod settings via: **Options → Mod Settings → Gestalt Engine Unlimited**

**BVR & General Settings:**
- **Gestalt Signal Relay Power**: Adjust power consumption (Default: 1000 W).
- **Smart Caretaker Check**: Enable/Disable safety logic for Caretakers in containers.

**Linear Mode Parameters:**
- Power Slope: 0-10,000W per level
- Bandwidth Slope: 0-30 per level
- Control Group Slope: 0-5 per level
- Research Speed Slope: 0-0.5 per level
- Heat Slope: 0-100 per level
- Upgrade Cooldown: 0.1-60 days
- Upgrade Duration: 0.1-30 days

**Logarithmic Mode Parameters:**
- Power Growth Base: 1.1-5.0
- Bandwidth Growth Base: 1.1-5.0
- Heat Growth Base: 1.1-5.0
- Min Bandwidth per Level: 0-10
- Cooldown Increment: 0.01-10 days/level
- Max Upgrade Cooldown: 0.1-60 days
- Duration Increment: 0.01-2 days/level
- Max Upgrade Duration: 0.1-30 days

### Technical Details
This mod uses Harmony patches to dynamically extend the `CompUpgradeable.Props.upgrades` list at runtime, generating upgrade data on-demand based on the selected curve mode and user settings.

### Compatibility
- Safe to add to existing saves
- Safe to remove (buildings will retain their current level)
- Compatible with most mods

### Source Code
Available on [GitHub](https://github.com/FruityMaxine/GestaltEngineUnlimited)

### License
MIT License - Feel free to modify and redistribute

### Author
**Fruity Trump**

---

## 简体中文

### 模组介绍
一个环世界(RimWorld)模组，解除格式塔枢纽的4级上限，允许理论上无限升级，并提供可自定义的增长曲线。

### 远程通信扩展 (BVR Extension)
- **超视距信号传输**：
  - **格式塔信号中继器**：新增建筑，用于极大扩展格式塔网络的信号范围。
  - **跨地图控制**：只要在信号覆盖范围内，机械体可以在世界地图或其他聚落进行活动。
  - **远程入侵**：在基地即可对远处地图的机械体进行远程骇入。
  - **远征支持**：格式塔控制的机械体现在可以正常登入穿梭机并加入远征队，不再因为“等级过低”报错。

### 功能特性
- **无限升级**：突破原版4级限制
- **双曲线模式**：
  - **线性模式**：固定斜率持续增长，每级提升相同数值
  - **对数模式**（推荐）：收束型曲线，游戏平衡性更好
- **完全可配置参数**：
  - 电力消耗增长
  - 机械带宽增长
  - 控制组数量增长
  - 研究速度增长
  - 热量产生增长
  - 升级冷却时间与持续时间
  - **新增**：格式塔信号中继器电力消耗（可调节 0-50,000 W）
- **智能安全检查**：防止独角仙(Caretaker)在穿梭机/空投舱内自爆。
- **开发者工具**：即时冷却按钮（仅开发者模式）、重置骇入冷却。
- **完整本地化**：英文 & 简体中文

### 前置要求
- 环世界 1.6
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)
- [Vanilla Expanded Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=1854607105)
- [Reinforced Mechanoid 2](https://steamcommunity.com/sharedfiles/filedetails/?id=3541404610)

### 安装方法
1. 在Steam创意工坊订阅（即将发布）
2. 或手动下载解压到 `RimWorld/Mods/` 文件夹
3. 在模组列表中启用（加载顺序：在前置模组之后）

### 参数配置
通过以下路径访问模组设置：**选项 → 模组设置 → 格式塔枢纽无限升级**

**BVR 与通用设置：**
- **格式塔信号中继器电力**：调节中继器的耗电量（默认 1000 W）。
- **智能独角仙检查**：开启/关闭对独角仙在容器内自爆的逻辑拦截。

**线性模式参数：**
- 电力斜率：0-10,000W/级
- 带宽斜率：0-30/级
- 控制组斜率：0-5/级
- 研究速度斜率：0-0.5/级
- 热量斜率：0-100/级
- 升级冷却时间：0.1-60天
- 升级持续时间：0.1-30天

**对数模式参数：**
- 电力增长底数：1.1-5.0
- 带宽增长底数：1.1-5.0
- 热量增长底数：1.1-5.0
- 每级最小带宽保证：0-10
- 冷却时间增量：0.01-10天/级
- 最大冷却时间上限：0.1-60天
- 持续时间增量：0.01-2天/级
- 最大持续时间上限：0.1-30天

### 技术实现
本模组使用Harmony补丁在运行时动态扩展 `CompUpgradeable.Props.upgrades` 列表，根据选择的曲线模式和用户设置按需生成升级数据。

### 兼容性
- 可安全添加到现有存档
- 可安全移除（建筑物会保持当前等级）
- 与大多数模组兼容

### 源代码
可在 [GitHub](https://github.com/FruityMaxine/GestaltEngineUnlimited) 查看

### 开源协议
MIT License - 欢迎修改和再分发

### 作者
**Fruity Trump**
