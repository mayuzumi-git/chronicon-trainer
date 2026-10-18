$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
    & 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' /nologo /target:winexe /platform:x64 /optimize+ /out:Chronicon修改器.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Trainer.cs MainWindow.cs KillMaterialEngine.cs KillMaterialProfile.cs Profile.cs InventoryEngine.cs InventoryProfile.cs MaterialDropProfile.cs PickupEngine.cs PickupProfile.cs InventoryWindow.cs ItemCatalog.cs RuneCatalog.cs EnchantEngine.cs EnchantProfile.cs EnchantItem.cs EnchantCatalog.cs EnchantWindow.cs MapEngine.cs MapProfile.cs MapCatalog.cs MapWindow.cs
    if ($LASTEXITCODE -ne 0) { throw '编译失败' }
} finally { Pop-Location }
