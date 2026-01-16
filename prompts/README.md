# AI Agent Prompts

Bu dizin, AIDevelopmentEasy agent'larının sistem prompt'larını içerir. Her prompt dosyası Markdown formatındadır ve agent'lar tarafından çalışma zamanında okunur.

## Prompt Dosyaları

| Dosya | Agent | Açıklama |
|-------|-------|----------|
| `planner.md` | PlannerAgent | Gereksinimleri analiz eder ve görev listesi oluşturur |
| `multi-project-planner.md` | MultiProjectPlannerAgent | Çoklu proje gereksinimlerini planlar |
| `coder-csharp.md` | CoderAgent (C#) | C# kodu üretir |
| `coder-generic.md` | CoderAgent (Other) | Diğer diller için kod üretir |
| `debugger-csharp.md` | DebuggerAgent (C#) | C# kodundaki hataları tespit eder ve düzeltir |
| `debugger-generic.md` | DebuggerAgent (Other) | Diğer dillerdeki hataları düzeltir |
| `reviewer.md` | ReviewerAgent | Kod kalitesini ve gereksinimlere uygunluğu değerlendirir |

## Prompt Düzenleme

Prompt'ları düzenlemek için:

1. İlgili `.md` dosyasını açın
2. İçeriği düzenleyin (Markdown formatında)
3. Dosyayı kaydedin

Değişiklikler **yeniden başlatma gerektirmeden** bir sonraki agent çağrısında otomatik olarak yüklenir.

## Değişken Kullanımı

Bazı prompt dosyalarında `{{VARIABLE}}` formatında değişkenler kullanılabilir:

- `coder-generic.md`: `{{LANGUAGE}}` - Hedef programlama dili

## Yapı

Prompt dosyaları şu yapıyı takip eder:

```markdown
# Agent Adı System Prompt

Açıklama paragrafı...

## Sorumluluklar

1. Madde 1
2. Madde 2

## Kurallar

- Kural 1
- Kural 2

## Çıktı Formatı

Beklenen çıktı formatı açıklaması...

**IMPORTANT**: Önemli notlar...
```

## Dikkat Edilecekler

1. **JSON Çıktısı**: Planner ve Reviewer agent'ları JSON formatında çıktı bekler
2. **Kod Çıktısı**: Coder ve Debugger agent'ları markdown code block içinde kod bekler
3. **Dil Uyumluluğu**: .NET Framework 4.6.2 uyumluluğuna dikkat edin
4. **Test Framework**: NUnit ve FluentAssertions kullanılır
