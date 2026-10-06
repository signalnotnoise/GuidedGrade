# LLM Settings System

## 🎯 What's New

You now have a complete **LLM configuration system** with GUI!

## 📍 How to Access

**Tools → LLM Configuration...**

## ⚙️ Features

### 1. Provider Selection
Choose between:
- **Ollama** (Local, free, private)
- **Azure OpenAI** (Cloud, powerful, paid)
- **OpenAI** (Coming soon)

### 2. Ollama Settings
- **Server URL**: Default `http://localhost:11434`
- **Model dropdown**: Pre-populated with popular models
  - qwen2.5:3b (fastest, recommended)
  - qwen3.5:9b
  - qwen3.6:27b
  - codellama:7b/13b
  - deepseek-coder:6.7b
  - phi4
  - llama3.3:70b
- **Refresh Models button**: Fetches actually installed models from your Ollama server
- **Status indicator**: Shows connection status and model count

### 3. Azure OpenAI Settings
- **Endpoint URL**: Your Azure OpenAI resource URL
- **API Key**: Secure password field
- **Deployment Name**: Your model deployment
- **Test Connection button**: Validates credentials

### 4. OpenAI Settings (Placeholder)
- **API Key**: For direct OpenAI access
- **Model**: gpt-4, gpt-4o, gpt-3.5-turbo, etc.
- Coming soon!

### 5. Requirements Template
- **Default requirements text** that gets sent to the LLM
- Can be customized per analysis type
- Saved across sessions

## 💾 Data Storage

Settings are saved to:
```
%APPDATA%\GuidedGrade\llm-settings.json
```

The first launch after the product rename migrates the previous settings directory
to `%APPDATA%\GuidedGrade`. Existing files in the new directory take precedence
if both locations are present. Saved guest credential and published runner paths
under the previous `%LOCALAPPDATA%` product directory are rewritten to their
migrated `GuidedGrade` locations when those destinations exist.

Secure storage for:
- ✅ Provider choice
- ✅ Model selection
- ✅ API keys (plaintext for now; TODO: encrypt)
- ✅ URLs and deployment names
- ✅ Requirements templates

## 🚀 Usage Flow

### First Time Setup

1. **Open app** → Tools → LLM Configuration
2. **Choose provider**:
   - **Ollama**: Click "Refresh Available Models", select one, Save
   - **Azure**: Enter endpoint, API key, deployment, Test, Save
3. **Click Save**
4. **Analyze files**: Right-click → Analyze Selected Files with LLM
5. **No more prompts!** It uses your saved settings automatically

### Daily Usage

1. **Check files** you want analyzed ☑
2. **Right-click** in tree → Analyze Selected Files with LLM
3. **App automatically uses** your configured provider & model
4. **Get feedback!**

## 🔄 Switching Providers

Want to try Azure vs Ollama?

1. Tools → LLM Configuration
2. Click different provider radio button
3. Save
4. Run analysis again - now uses new provider!

## 🧪 Testing Your Setup

### Ollama Test
1. **Refresh Available Models** button
2. Should show: `✓ Found X model(s)`
3. If error: Start Ollama service

### Azure Test
1. Fill in all three fields
2. Click **Test Connection**
3. Should show: `✓ Connection successful!`
4. If error: Check endpoint URL, API key, deployment name

## 📊 Model Recommendations

| Model | Provider | Speed | Quality | RAM | Cost |
|-------|----------|-------|---------|-----|------|
| **qwen2.5:3b** | Ollama | ⚡⚡⚡ | Good | 8GB | Free |
| qwen3.5:9b | Ollama | ⚡⚡ | Very Good | 16GB | Free |
| codellama:7b | Ollama | ⚡⚡ | Good (code) | 12GB | Free |
| **gpt-4o** | Azure | ⚡⚡⚡ | Excellent | N/A | ~$0.03 |
| gpt-4 | Azure | ⚡⚡ | Excellent | N/A | ~$0.05 |

**Bold** = Recommended defaults

## 🔐 Security Notes

**Current**:
- API keys stored in plaintext JSON
- File permissions: user-only read/write

**TODO**:
- Encrypt API keys using DPAPI
- Add master password option
- Secure credential manager integration

**For now**: Don't commit `llm-settings.json` to Git!

## 🐛 Troubleshooting

### "Ollama Not Available"
```bash
# Check if Ollama is running
ollama list

# If no models shown:
ollama pull qwen2.5:3b

# If service not running (Windows):
# Open Services → Start "Ollama"
```

### "Azure configuration required"
- Open Tools → LLM Configuration
- Fill in all Azure fields
- Click Test Connection
- Save

### Settings not saving
- Check write permissions to `%APPDATA%`
- Look for error messages in Debug output
- Try running as admin (not recommended long-term)

### Model timeout
- Switch to smaller model (qwen2.5:3b)
- Increase timeout in code (currently 15 min)
- Check CPU usage during analysis

## 🎨 UI Layout

```
┌─────────────────────────────────────┐
│ LLM Configuration                   │
├─────────────────────────────────────┤
│ Provider: ○ Ollama ○ Azure ○ OpenAI │
├─────────────────────────────────────┤
│ [Provider-specific settings]        │
│   - Ollama: URL + Model dropdown    │
│   - Azure: Endpoint + Key + Deploy  │
│   - OpenAI: Key + Model             │
│                                     │
│ [Test/Refresh Buttons]              │
│ Status: ✓ Ready                     │
├─────────────────────────────────────┤
│ Requirements Template:              │
│ [Large text box]                    │
├─────────────────────────────────────┤
│              [Save]    [Cancel]     │
└─────────────────────────────────────┘
```

## 📝 Next Steps

1. ✅ **Settings UI** - DONE!
2. ⏳ **Encrypt API keys** - TODO
3. ⏳ **Requirements library** - Multiple templates
4. ⏳ **Per-assignment requirements** - Auto-detect from folder
5. ⏳ **Batch analysis** - Analyze multiple students at once
6. ⏳ **OpenAI direct integration** - Add OpenAIService.cs
7. ⏳ **Model auto-selection** - Recommend based on hardware

## 🎉 Summary

You can now:
- ✅ Choose LLM provider from GUI
- ✅ Select from available models
- ✅ Test connections before saving
- ✅ Store settings persistently
- ✅ Switch providers anytime
- ✅ No more hardcoded credentials!

**Ready to use!** Configure via Tools → LLM Configuration, then analyze away! 🚀

## Request preferences (October 5, 2026)

Open Settings > AI Provider to configure Console model wait timeout. It accepts whole seconds from 1 to 90 and defaults to 30. It controls each interactive console decision, including waiting in the shared model queue, and is still capped by the remaining overall 90-second test budget. It does not change grading response timeouts.

Ask before each local build or run is respected by Build, Build and Run, Run, Test with AI and Test with AI locally. The explicit local command changes only the environment for that request. Ask before grading controls the start confirmation for overall reviews, section batches, selected sections, regeneration and post-test grading. Both toggles are also available in Settings > Programming Checks. Save to persist them; settings take effect for newly started requests. Old settings default to asking, and invalid saved timeout values fall back to 30 seconds.