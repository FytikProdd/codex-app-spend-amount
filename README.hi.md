# Codex Cost Tracker

[English version](./README.md) | [Русская версия](./README.ru.md)

Windows के लिए एक कॉम्पैक्ट WinUI 3 डेस्कटॉप ऐप, जो हर चैट के लिए Codex/Codex App लागत को ट्रैक करता है और लगभग रियल टाइम में अनुमानित उपयोग लागत दिखाता है।

## यह क्या करता है

- `~/.codex/sessions` और `~/.codex/archived_sessions` की निगरानी करता है
- आधिकारिक OpenRouter Models API से प्राइसिंग डेटा प्राप्त करता है
- टोकन उपयोग रिकॉर्ड के आधार पर प्रति-चैट और कुल खर्च का अनुमान लगाता है
- सेशन फ़ाइल बदलने पर अपने-आप रिफ्रेश होता है
- UI से चैट और कीमतों को मैन्युअली रिफ्रेश करने देता है

## आवश्यकताएँ

- Windows 10 संस्करण 1809 या नया
- लोकल बिल्ड के लिए .NET 9 SDK
- OpenRouter प्राइसिंग सिंक के लिए इंटरनेट कनेक्शन

## जल्दी शुरू करें

प्रकाशित ऐप चलाएँ:

```powershell
powershell -ExecutionPolicy Bypass -File .\Start-CodexSpendPopout.ps1
```

लॉन्च से पहले नया publish जबरन करें:

```powershell
powershell -ExecutionPolicy Bypass -File .\Start-CodexSpendPopout.ps1 -Build
```

## सोर्स से बिल्ड करें

प्रोजेक्ट बिल्ड करें:

```powershell
dotnet build .\CodexSpendMonitor\CodexSpendMonitor.csproj
```

self-contained executable publish करें:

```powershell
dotnet publish .\CodexSpendMonitor\CodexSpendMonitor.csproj -c Release -o .\dist\CodexSpendMonitor
```

रन होने योग्य लोकल `dist/` फ़ोल्डर के लिए लॉन्चर स्क्रिप्ट का उपयोग करना बेहतर है, क्योंकि यह publish आउटपुट में आवश्यक WinUI generated resources भी सिंक करती है:

```powershell
powershell -ExecutionPolicy Bypass -File .\Start-CodexSpendPopout.ps1 -Build
```

## रिपॉज़िटरी संरचना

- `CodexSpendMonitor/` - WinUI 3 ऐप का सोर्स कोड
- `Start-CodexSpendPopout.ps1` - लोकल publish + run के लिए सुविधा स्क्रिप्ट
- `dist/` - generated publish आउटपुट, Git द्वारा अनदेखा
- `dumps/` और `*.log` - लोकल डायग्नोस्टिक्स, Git द्वारा अनदेखा

## खर्च का अनुमान कैसे लगाया जाता है

ऐप सेशन `.jsonl` फ़ाइलें पढ़ता है और इन मानों को मिलाता है:

- `input_tokens`
- `cached_input_tokens`
- `output_tokens`
- `reasoning_output_tokens`

साथ में, यह इनसे प्राइसिंग लेता है:

- [OpenRouter Models API](https://openrouter.ai/api/v1/models)

अगर किसी सेशन मॉडल को OpenRouter प्राइस एंट्री से मैच नहीं किया जा सकता, तो भी चैट UI में दिखेगी, लेकिन उसकी कीमत unmatched के रूप में चिह्नित होगी।

## सेशन फ़ोल्डर खोज

डिफ़ॉल्ट रूप से ऐप यह पाथ मॉनिटर करता है:

- `~/.codex/sessions`
- `~/.codex/archived_sessions`

अगर किसी मशीन पर Codex सेशन किसी और जगह स्टोर करता है, तो आप environment variables से path discovery override कर सकते हैं:

- `CODEX_HOME` - parent फ़ोल्डर माना जाता है, जिसमें `sessions/` और `archived_sessions/` होते हैं
- `CODEX_SESSIONS_DIR` - live sessions फ़ोल्डर का explicit path
- `CODEX_ARCHIVED_SESSIONS_DIR` - archived sessions फ़ोल्डर का explicit path
