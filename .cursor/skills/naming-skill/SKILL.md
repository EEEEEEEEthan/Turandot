---
name: naming-skill
description: 当需要给任何东西命名时必读
---

# 命名规范

## 核心原则

1. **使用英文命名**：所有标识符（类、方法、变量、属性、常量、字段）都使用英文
2. **命名要准确反映用途**：命名必须精确描述其功能或含义，不能有歧义
3. **鼓励长命名**：为了提高可读性，优先使用描述性强的长命名，不要为了简短而牺牲清晰度
4. **中世纪风格命名**：这是一个中世纪题材的游戏，所有命名都可以带有中世纪风味（如使用medieval、knight、castle、realm、throne等词汇）

## 命名规则

1. 字段属性常量等，应该是名词性质的单词或短语
2. 底消歧义。例如time->localTime/utcTime; duration->durationSeconds；min->minIncluded；max->maxExcluded；position->localPosition/globalPosition等
3. **图片命名要描述内容而不是用途**：图片文件名应该描述图片的视觉内容（颜色、形状、特征等），而不是其使用场景。例如：一个用于按钮的图片，看上去是一个橙色有边框的矩形，应该命名为`OrangeBoundedRect.png`而不是`Button.png`
4. **重命名文件时同步重命名.uid文件**：如果被重命名的文件带有对应的.uid文件（如Godot场景文件.tscn对应的.uid文件），需要将.uid文件一同重命名，保持文件名一致

## 命名检查清单

在命名时，确保：

- ✅ 命名准确描述了用途，没有歧义
- ✅ 命名足够具体，不会与其他概念混淆
- ✅ 使用英文命名
- ✅ 方法名使用动词，变量/属性名使用名词
- ❌ 不要为了简短而使用缩写或模糊的命名

## 示例对比

**好的命名：**
```csharp
const float RotationSpeed = 2.0f;
public void SetPositionImmediately(Vector3 position)
Vector3 ClampToMapBounds(Vector3 position)
readonly SmoothVector3 smoothPosition = new();
```

**不好的命名：**
```csharp
const float rs = 2.0f;  // 缩写不清晰
public void set(Vector3 pos)  // 不够具体
Vector3 limit(Vector3 p)  // 参数名不清晰
```
