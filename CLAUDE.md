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
| METRAJTOPLA | Quick number sum |

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

## Yol Enkesit Okuma Modülü (Aktif Geliştirme)

İhale DWG dosyalarından enkesit verilerini yarı-otomatik okuyan bağımsız panel.
Plan dokümanı: `docs/YolEnkesitOkuma_Plan.md`

### Klasör Yapısı (yeni dosyalar)
- `Models/YolEnkesit/` → AnchorNokta, CizgiRolu, CizgiTanimi, KesitPenceresi, ReferansKesitSablonu, KesitGrubu, AlanHesapSonucu, TabloKiyasSonucu
- `Services/YolEnkesit/` → AnchorTaramaService, KesitGruplamaService, CizgiRolAtamaService, KesitAlanHesapService, TabloOkumaService
- `ViewModels/EnkesitOkuma/` → EnkesitOkumaMainViewModel, ReferansKesitViewModel, KesitDogrulamaViewModel
- `Views/EnkesitOkuma/` → EnkesitOkuMainControl, ReferansKesitWindow, KesitDogrulamaWindow, KesitOnizlemeControl
- `Commands/` → EnkesitOkuCommands.cs, EnkesitOkuPaletteManager.cs

### Mevcut Kodda Değişiklikler (minimum müdahale)
- `EnKesitAlanService.cs`: IkiCizgiArasiAlan, ClipToXRange, InterpolateY, ShoelaceAlan → public yap
- `YolKesitService.cs`: IstasyonParse, IstasyonFormatla → public static yap
- `Infrastructure/ServiceContainer.cs`: Yeni servislerin DI kaydı
- `Commands/RibbonManager.cs`: "Enkesit Oku" ribbon butonu ekle

### Dokunma
- MetrajMainControl.xaml — tab ekleme, değişiklik yapma
- YolMetrajViewModel, YolKesitService, HatchOlusturmaService — olduğu gibi kalacak
- Mevcut tüm Views/ ve ViewModels/ dosyaları

### Alan Hesap Kuralları (çizgi çiftleri)
| Malzeme | Üst Çizgi | Alt Çizgi | Not |
|---|---|---|---|
| Sıyırma | Zemin | SiyirmaTaban | Her zaman |
| Kazı (Yarma) | SiyirmaTaban | UstyapiAltKotu | SıyırmaTaban > UstyapiAlt olduğu X aralığı |
| Dolgu | UstyapiAltKotu | SiyirmaTaban | UstyapiAlt > SıyırmaTaban olduğu X aralığı |
| B.T. Yerine Konan | Sıyırma alanı | — | Dolgu bölgesindeki sıyırma |
| B.T. Yerine Konmayan | Sıyırma alanı | — | Yarma bölgesindeki sıyırma |
| Üstyapı tabakaları | Üst tabaka çizgisi | Alt tabaka çizgisi | ProjeCizgisi→Aşınma→Binder→Bitümlü→Plentmiks→Alttemel→Kırmataş |

### Geliştirme Fazları
- Faz 1: Models + AnchorTaramaService + unit testler
- Faz 2: KesitPenceresi + KesitGruplamaService
- Faz 3: KesitOnizlemeControl + ReferansKesitWindow + CizgiRolAtamaService
- Faz 4: KesitAlanHesapService + otomatik eşleşme
- Faz 5: TabloOkumaService + kıyaslama
- Faz 6: KesitDogrulamaWindow + EnkesitOkuMainControl + Excel çıktı
