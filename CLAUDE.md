# CLAUDE.md

This file provides guidance to Claude Code when working with the Metraj project.
- Tüm çıktılarını Türkçe yaz: düşünme, açıklama, commit mesajları, todo güncellemeleri — HEPSİ Türkçe.
- Bir sınıf 1000+ koda ulaştığında artık üzerine kod yazma. Yeni partial class oluştur (ClassName.Aspect.cs).
- Build yapmak için onay alma. Build direkt yap. AutoCAD açıkken DLL kitli olabilir.
- Düzeltmeleri ve kod yazma işlemi bittikten sonra yaptığın açıklamaları benim anlayacağım şekilde yap.

## Project Overview

Metraj is an AutoCAD/Civil 3D extension (DLL) for quantity surveying (metraj/ölçü alma).
Measures length, area, volume from drawing entities and sums numbers from text objects.
Works in both plain AutoCAD 2025 and Civil 3D 2025.

## Build & Test

dotnet build
dotnet test --project Metraj.Tests

- .NET 8.0 SDK, x64
- AutoCAD 2025 API DLLs from C:\Program Files\Autodesk\AutoCAD 2025\
- Civil 3D components (AeccDbMgd, AecBaseMgd) optional — runtime detected via Civil3dDetector
- NuGet: ClosedXML, Microsoft.Extensions.DependencyInjection, Newtonsoft.Json, Serilog

## Architecture

**Pattern:** MVVM with service layer + DI Container

Commands/           → AutoCAD command entry points (IExtensionApplication)
Infrastructure/     → DI container (ServiceContainer) + AutoCAD abstraction layer
  └── AutoCAD/      → IDocumentContext, IEditorService, IEntityService, Civil3dDetector
Models/             → Data models (measurement results, settings, enums)
Services/           → Business logic (calculation, annotation, export)
  └── Interfaces/   → Service contracts
ViewModels/         → MVVM ViewModels with INotifyPropertyChanged
Views/              → WPF UserControls (XAML + code-behind)
  ├── Styles/       → Shared XAML styles
  └── Converters/   → Value converters

### DI Container (ServiceContainer)

- **Singleton:** Civil3dService, all ViewModels
- **Transient:** IDocumentContext, IEditorService, IEntityService, calculation services
- Initialized in MetrajCommands.Initialize()
- Access: ServiceContainer.GetRequiredService<T>()

### Civil 3D Runtime Detection

Civil3dDetector.IsCivil3dAvailable() checks assembly load once.
Civil 3D partial class files (*.Civil3d.cs) contain all Civil 3D-specific code.

### Partial Class Convention

Large services split into partial files: ClassName.Aspect.cs

## AutoCAD Commands

| Command | Description |
|---------|-------------|
| METRAJ | Toggle main panel |
| METRAJUZUNLUK | Quick length measurement |
| METRAJALAN | Quick area measurement |
| METRAJHACIM | Volume tab |
| METRAJTOPLA | Quick number sum |
| METRAJANNOTASYON | Write annotation |
| METRAJEXCEL | Export to Excel |
| METRAJTEMIZLE | Clear annotations |

## Coding Standards

- Turkish for domain terms: Uzunluk, Alan, Hacim, Toplama, Enkesit, Istasyon
- English for programming concepts: Service, ViewModel, Command
- INotifyPropertyChanged for all ViewModels (base: ViewModelBase)
- Nullable disabled, ImplicitUsings disabled
- All AutoCAD operations inside Transaction
- Turkish user-facing error messages
- Serilog logging: LoggingService.Info/Warning/Error

## Loading

NETLOAD → Metraj.dll seç
