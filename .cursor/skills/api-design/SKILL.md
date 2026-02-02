---
name: api-design
description: 设计清晰的公共API，封装内部状态，避免歧义，提供统一接口。当需要设计类的方法、封装内部状态、设计公共接口时使用。
---

# API设计原则

## 核心原则

### 1. 封装内部状态

**原则**：内部实现细节应该是私有的，只暴露必要的公共API。

**错误示例**：
```csharp
public class CameraController
{
    public Node3D? TrackingTarget;  // ❌
    public Vector3 TargetPosition { get; set; }  // ❌
    // 当 TrackingTarget 不为 null 时，会不断覆盖 TargetPosition
    // 导致两个API产生冲突 TargetPosition 的 setter/getter 与命名直觉不一致
}
```

**正确示例**：
```csharp
public class CameraController
{
    // ✅ 内部状态私有化
    private Node3D? TrackingTarget { get; set; }
    private Vector3 TargetPosition { get; set; }

    // ✅ 提供清晰的公共API，表达意图而非实现
    public void Track(Node3D? node);       // 跟踪节点（null表示停止跟踪）
    public void LookAt(Vector3 position);  // 看向位置（固定位置，不跟踪）
}
```

**为什么更好**：
- `Track()` 和 `LookAt()` 的语义完全不同，不会产生冲突
- 调用者不需要了解内部 `TrackingTarget` 和 `TargetPosition` 的关系
- API行为与命名一致，符合直觉

### 2. 避免歧义

**原则**：API的行为应该明确，避免多种方式做同一件事导致歧义。

**错误示例**：
```csharp
public class CameraController
{
    public Vector3 TargetPosition { get; set; }  // 方式1：设置位置
    public Node3D? TrackingTarget { get; set; }  // 方式2：设置跟踪目标
    // ❌ 两种方式都会影响相机位置，但存在冲突
    // 用户不知道应该用哪个，也不知道它们之间的关系
}
```

**正确示例**：
```csharp
public class CameraController
{
    // ✅ 明确的行为：跟踪目标（相机会跟随目标移动）
    public void Track(Node3D? node) { /* ... */ }

    // ✅ 明确的行为：看向位置（自动取消跟踪）
    public void LookAt(Vector3 position) { /* ... */ }
}
```

**为什么更好**：
- `Track()` 和 `LookAt()` 的意图完全不同，不会混淆
- `Track(null)` 可以停止跟踪，不需要单独的 `StopTracking()` API
- 调用者不需要了解内部状态的关系

### 3. 统一接口设计

**原则**：提供重载方法处理不同类型的输入，但保持一致的语义。

**错误示例**：
```csharp
public class CameraController
{
    // ❌ 方法名不一致，语义混乱
    public void FollowNode(Node3D node);
    public void TrackLocation(LocationAttachment location);
    public void SetPosition(Vector3 position);
}
```

**正确示例**：
```csharp
public class CameraController
{
    // ✅ 统一的方法名，不同的参数类型
    public void Track(Node3D node) { /* ... */ }
    public void Track(LocationAttachment location) { /* ... */ }
    public void LookAt(Vector3 position) { /* ... */ }
}
```

**为什么更好**：
- 所有跟踪操作都使用 `Track()` 方法，语义一致
- 调用者不需要记住不同的方法名

### 4. 副作用明确化

**原则**：API的副作用应该在方法名或实现中明确表达。

**正确示例**：
```csharp
public class CameraController
{
    // ✅ 明确：LookAt会取消跟踪（副作用在实现中体现）
    public void LookAt(Vector3 position)
    {
        Track(null);  // 副作用明确：先取消跟踪
        TargetPosition = position;
    }
}
```

**为什么更好**：
- 调用者可以清楚知道 `LookAt()` 会取消跟踪
- 副作用在方法内部统一处理，避免调用者遗漏

### 5. 避免冗余API

**原则**：如果现有API已经能表达某个操作，不需要添加新的API。

**错误示例**：
```csharp
public class CameraController
{
    public void Track(Node3D node) { /* ... */ }
    public void StopTracking() { /* ... */ }  // ❌ 冗余：Track(null) 就能满足
}
```

**正确示例**：
```csharp
public class CameraController
{
    // ✅ Track(null) 可以停止跟踪，不需要单独的 StopTracking() API
    public void Track(Node3D? node) { /* ... */ }
}
```

**为什么更好**：
- 减少API数量，降低学习成本
- 统一的接口更易理解：`Track()` 处理所有跟踪相关的操作
- `null` 作为"无目标"的语义清晰自然

**注意**：如果 `Track()` 有多个重载，`Track(null)` 可能产生语法歧义，此时可以保留 `StopTracking()` 方法。

## 实践指南

### 状态封装模式

**场景**：有内部状态需要控制，但不想暴露给外部

```csharp
public class StateManager
{
    private State _currentState;  // ✅ 私有状态

    // ✅ 公共方法控制状态，而不是直接暴露字段
    public void SetState(State newState)
    {
        if(_currentState != newState)
        {
            _currentState?.OnExit();
            _currentState = newState;
            _currentState?.OnEnter();
        }
    }
}
```

### 操作封装模式

**场景**：有复杂操作需要封装，包含多个步骤

```csharp
public class FileProcessor
{
    // ✅ 私有实现细节
    private void ValidateFile(string path) { /* ... */ }
    private void ProcessContent(string content) { /* ... */ }

    // ✅ 公共方法封装完整流程
    public void ProcessFile(string path)
    {
        ValidateFile(path);
        var content = ReadFile(path);
        ProcessContent(content);
    }
}
```

### 统一接口模式

**场景**：需要处理多种输入类型，但执行相同的操作

```csharp
public class DataProcessor
{
    // ✅ 统一的方法名，不同的参数类型
    public void Process(string data) { ProcessCore(ParseString(data)); }
    public void Process(int data) { ProcessCore(data.ToString()); }
    public void Process(DataObject data) { ProcessCore(data.Serialize()); }

    private void ProcessCore(string normalizedData) { /* 统一逻辑 */ }
}
```

## 检查清单

设计API时，检查：

- [ ] 内部状态是否私有？
- [ ] 是否避免了歧义（避免多种方式做同一件事）？
- [ ] 接口是否统一（相关功能使用统一的方法名）？
- [ ] 副作用是否明确（在方法名或实现中体现）？
- [ ] 方法名是否清晰表达了意图？
- [ ] 是否避免了冗余API（现有API能否通过参数值表达特殊状态）？
- [ ] 是否处理了所有合理的输入类型（包括null/空值）？
