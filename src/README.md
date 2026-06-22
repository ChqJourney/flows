# 标准库维护 Web App

一个本地运行的 Web 应用，用于维护标准文档库。将 PDF 标准转换为 Markdown + 图片后，通过本应用上传、归档、管理和查阅。

## 功能

- **上传归档**：接收已转换好的 zip 包（内含 `.md` 文件和 `images/` 图片文件夹），按标准号自动分类存放。
- **自动识别标准号**：从 zip 文件名自动提取标准号，支持手动修改。
- **状态管理**：标记标准为"现行 / 被替代 / 废止 / 草案"。
- **查重提示**：基于 zip 文件 SHA256 和标准号+版本双重查重，发现重复时弹出提示，用户确认后仍可上传。
- **Markdown 查看**：支持 KaTeX 公式渲染和图片显示。
- **本地存储**：SQLite 保存索引，文件系统保存标准文档本体。

## 技术栈

- **.NET 10** + **ASP.NET Core Blazor Web App**（服务端交互渲染）
- **SQLite** + **Entity Framework Core**
- **Markdig**（Markdown 渲染）
- **KaTeX**（公式渲染，已下载到 `wwwroot/lib/katex/`，离线可用）

## 项目结构

```
src/StandardLibrary.Web/
├── Components/              # Blazor 页面和组件
│   ├── Pages/               # 页面：首页、上传、详情、查看
│   └── Layout/              # 布局
├── Data/                    # DbContext
├── Models/                  # 数据模型
├── Services/                # 业务服务
│   ├── StandardArchiveService.cs   # 上传归档核心逻辑
│   ├── MarkdownRendererService.cs  # Markdown 渲染
│   └── StandardSlugService.cs      # 标准号解析
├── wwwroot/lib/katex/       # 本地 KaTeX 资源
├── standards/               # 标准文档仓库（运行时生成）
├── standards.db             # SQLite 数据库（运行时生成）
└── Program.cs               # 应用入口
```

## 如何运行

```bash
cd src/StandardLibrary.Web
dotnet run --urls "http://localhost:5210"
```

浏览器打开：`http://localhost:5210`

## 使用流程

1. 用 Marker / MinerU 等工具将 PDF 标准转换为 Markdown + images。
2. 将产物打包成 zip，建议文件名包含标准号，例如 `GB-T-1234.1-2020.zip`。
3. 打开本应用上传页面，选择 zip，核对自动识别的标准号，选择状态。
4. 点击"上传并归档"，系统自动创建文件夹并入库。
5. 在列表页点击"查看"阅读标准正文。

## 归档规则

标准文件按以下规则存入 `standards/` 目录：

```
standards/{Slug}_{Status}/
├── standard.md
└── images/
    └── xxx.png
```

例如：

```
standards/GB-T-1234.1-2020_current/
```

- `Slug`：标准号转义后的文件系统安全形式，如 `GB-T-1234.1-2020`。
- `Status`：`current`（现行）、`superseded`（被替代）、`repealed`（废止）、`draft`（草案）。

## 注意事项

- PDF 转 Markdown 不在本项目范围内，需使用外部工具（如 Marker）预先转换。
- 旧数据（加文件哈希查重之前上传的）没有 `SourceFileHash`，对这些旧 zip 再次上传不会触发文件重复提示，但仍会触发标准号+版本查重。
- 本应用主要面向本地单机使用，未提供对外 API。
