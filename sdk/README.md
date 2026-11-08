# 编译期依赖

这个目录用来放编译插件时引用的两个启动器程序集，它们**不随仓库分发**（版权归 PCL Nex 所有）：

```
sdk/current/PCL.Core.dll
sdk/current/Plain Craft Launcher 2.dll
```

## 获取方式

从你本机安装的 PCL Nex 中复制，或者从 [PCL Nex 发布页](https://github.com/PCL-Nex-Developer/PCL2-Nex/releases) 下载对应版本后取出这两个 DLL。

## 编译

```powershell
pwsh tools/build.ps1
```

脚本默认读 `sdk/current/`；DLL 放在别处时用 `-SdkDir` 指定：

```powershell
pwsh tools/build.ps1 -SdkDir "D:\PCL\lib"
```

也可以直接 `dotnet build src/AdvancedHomepage/AdvancedHomepage.csproj`，项目文件里的默认路径同样指向 `sdk/current/`。
