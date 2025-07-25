<a id="readme-top"></a>

[![netstandard2.0](https://img.shields.io/badge/netstandard-2.0-blue.svg)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)
[![Avalonia](https://img.shields.io/badge/Avalonia-11-blue.svg)](https://avaloniaui.net/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![GitHub issues](https://img.shields.io/github/issues/DearVa/Avalonia.LiveMarkdown.svg)](https://github.com/DearVa/Avalonia.LiveMarkdown/issues)
[![NuGet](https://img.shields.io/nuget/v/Avalonia.LiveMarkdown.svg)](https://www.nuget.org/packages/Avalonia.LiveMarkdown/)

![demo.gif](https://raw.githubusercontent.com/DearVa/Avalonia.LiveMarkdown/main/img/demo.gif)

---

## About `Avalonia.LiveMarkdown`

`Avalonia.LiveMarkdown` is a High-performance Markdown viewer for Avalonia applications.
It supports **real-time rendering** of Markdown content, so it's ideal for applications that require dynamic text updating, **especially when streaming large model outputs**.

## ⭐ Features

- 🚀 **High-performance rendering powered by [Markdig](https://github.com/xoofo/markdig)**
- 🔄 **Real-time updates**: Automatically re-renders changes in Markdown content
- 🎨 **Customizable styles**: Easily style Markdown elements using Avalonia's powerful styling system
- 🔗 **Hyperlink support**: Clickable links with customizable behavior
- 📊 **Table support**: Render tables with proper formatting
- 📜 **Code block syntax highlighting**: Supports multiple languages with [ColorCode](https://github.com/CommunityToolkit/ColorCode-Universal)
- 🖼️ **Image support**: Load images asynchronously with [AsyncImageLoader.Avalonia](https://github.com/AvaloniaUtils/AsyncImageLoader.Avalonia)

> [!NOTE]
> This library currently only supports `Append` and `Clear` operations on the Markdown content, which is enough for LLM streaming scenarios.

---

## ✈️ Roadmap

- [x] Basic Markdown rendering
- [x] Real-time updates
- [x] Hyperlink support
- [x] Table support
- [x] Code block syntax highlighting
- [x] Image support
- [ ] Selectable text across elements
- [ ] LaTeX support
- [ ] HTML rendering

---

## 🚀 Getting Started

### 1. Install the NuGet package

You can install the latest version from NuGet CLI:

```bash
dotnet add package Avalonia.LiveMarkdown
```

or use the NuGet Package Manager in your IDE.

### 2. Register the Markdown styles in your Avalonia application

```xml
<Application
  x:Class="YourAppClass" xmlns="https://github.com/avaloniaui"
  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" RequestedThemeVariant="Default">

  <Application.Styles>
    <!-- Your other styles here -->
    <StyleInclude Source="avares://Avalonia.LiveMarkdown/Styles.axaml"/>
  </Application.Styles>

  <Application.Resources>
    <!-- Your other resources here -->
    <Color x:Key="BorderColor">#3DFFFFFF</Color>
    <Color x:Key="ForegroundColor">#FFFFFF</Color>
    <Color x:Key="CardBackgroundColor">#15000000</Color>
    <Color x:Key="SecondaryCardBackgroundColor">#99000000</Color>
  </Application.Resources>
</Application>
```

### 3. Use the `MarkdownView` control in your XAML

Add the `MarkdownView` control to your `.axaml` file:
```xml
<YourControl
  xmlns:md="clr-namespace:Avalonia.LiveMarkdown;assembly=Avalonia.LiveMarkdown">
  <md:MarkdownRenderer x:Name="MarkdownRenderer"/>
</YourControl>
```

Then you can manage the Markdown content in your code-behind:

```csharp
// ObservableStringBuilder is used for efficient string updates
var markdownBuilder = new ObservableStringBuilder();
MarkdownRenderer.MarkdownBuilder = markdownBuilder;

// Append Markdown content, this will trigger re-rendering
markdownBuilder.Append("# Hello, Markdown!");
markdownBuilder.Append("\n\nThis is a **live** Markdown viewer for Avalonia applications.");

// Clear the content
markdownBuilder.Clear();
```

---

## 🪄 Style Customization

Markdown elements can be styled using Avalonia's powerful styling system. You can override the [default styles](https://github.com/DearVa/Avalonia.LiveMarkdown/blob/main/src/Avalonia.LiveMarkdown/Styles.axaml) by defining your own styles in your application styles.

Avalonia Styling Docs: 
- [Avalonia Styles](https://docs.avaloniaui.net/docs/styling)
- [Style selector syntax](https://docs.avaloniaui.net/docs/reference/styles/style-selector-syntax)

---

## 🤝 Contributing

We welcome issues, feature ideas, and PRs! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

---

## 📄 License

Distributed under the Apache 2.0 License. See [LICENSE](LICENSE) for more information.

### Third-Party Licenses

- **markdig** - [BSD-2-Clause License](https://github.com/xoofx/markdig/blob/master/license.txt)
    - Markdown parser for Everywhere.Markdown rendering
    - Source repo: https://github.com/xoofx/markdig
- **AsyncImageLoader.Avalonia** - [MIT License](https://github.com/AvaloniaUtils/AsyncImageLoader.Avalonia/blob/master/LICENSE)
    - Asynchronous image loading for Avalonia
    - Source repo: https://github.com/AvaloniaUtils/AsyncImageLoader.Avalonia
- **ColorCode** - [MIT License](https://github.com/CommunityToolkit/ColorCode-Universal/blob/main/license.md)
    - Syntax highlighting for code blocks
    - Source repo: https://github.com/CommunityToolkit/ColorCode-Universal

---

<p align="right">(<a href="#readme-top">back to top</a>)</p>
