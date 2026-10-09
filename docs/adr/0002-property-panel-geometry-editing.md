# ADR 0002 — 属性面板几何编辑

**Date:** 2026-10-06

**Status:** Accepted

**Deciders:** 用户确认方案；Codex 实施与复审

**Proposal:** [0001 — 属性面板几何编辑](../proposals/0001-property-panel-geometry-editing.md)

## Context

现有 WPF 属性区通过 `PropertyShape` 和图形类型模板展示单选图形。矩形宽高和圆半径可直接编辑，但线段端点、长度和图形位置缺少完整的编辑行为。位置与尺寸有不同联动规则，输入中间状态及值类型坐标也不能直接作为图形更新契约。宿主可以使用自定义 `IImageViewerViewModel`，应避免为仅属于面板的输入草稿增加必需接口成员。

## Options Considered

### A — 几何编辑控件与明确的图形更新方法

- 控件内部维护字符串草稿，按起点、终点、位置、尺寸等逻辑组提交。
- 复用类型模板和暗色主题，图形层保持几何和绘制职责。
- 需要额外处理焦点事件、通知顺序和事件生命周期。

### B — 直接 TwoWay 绑定图形字段

- 改动少，适合普通标量。
- 不能保证完整 Point 更新、矩形位置保持大小、输入校验和派生状态一致。

### C — 扩展宿主 VM 或通用 PropertyGrid

- 可以集中列出大量属性。
- 扩张宿主必需接口，且通用 getter/setter 无法表达各图形的联动语义。

## Decision

使用 `Lan.ImageViewer.ShapeGeometryEditorControl` 和三个轻量编辑器承接输入，在 `Lan.Shapes` 提供明确的整体修改方法，纯位置修改复用 Repository 平移。

## Rationale

按逻辑组提交能让用户直接编辑数值，同时将不完整或非法文本留在草稿中。线段长度固定起点与方向，矩形位置保持尺寸，圆位置保持半径，规则独立且可测试。该方案保持 WPF 原生 `DrawingVisual` 架构，不引入新包，也无需修改 `IImageViewerViewModel`。

## Consequences

- **Positive:** 面板与鼠标操作共用真实图形模型；位置、大小、手柄、导出数据和通知有明确契约。
- **Negative / trade-offs:** 第一版仅支持内置三种独立单选图形，以 px 编辑；独立非恒等变换、派生类型和多选需要后续适配。
- **Risks:** 画布和树的焦点/改选顺序不同，失焦提交必须延后并核对目标及版本；静态 Rendering 和图形事件必须在卸载、换绑时退订。极端坐标不能表达目标位置或尺寸时拒绝提交。

## Related Decisions

- [ADR 0001 — WPF-native sketch architecture](0001-wpf-native-sketch-architecture.md)
