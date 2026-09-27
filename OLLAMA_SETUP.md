# Ollama Local LLM Setup Guide

## 🎯 What is Ollama?

Ollama is a **local AI server** that runs large language models on your computer. Benefits:
- ✅ **100% Free** - No API costs
- ✅ **100% Private** - Data never leaves your machine
- ✅ **Works Offline** - No internet required
- ✅ **Fast** - No network latency
- ✅ **FERPA Compliant** - Perfect for student data

## 📥 Installation

### Windows / macOS / Linux

1. **Download Ollama**: https://ollama.ai/download
2. **Install** - Just like any other app
3. **Verify** - Open terminal and run:
   ```bash
   ollama --version
   ```

## 🤖 Recommended Models for Code Analysis

### Qwen 3.6 27B ⭐ (Recommended for grading)
**Best balance of quality and speed**
```bash
ollama pull qwen3.6:27b
```
- Size: ~16GB
- RAM needed: 24GB+ recommended
- Quality: Excellent for code review
- Speed: ~10-20 tokens/sec on modern CPU

### Qwen 3.6 14B (For lower-end machines)
```bash
ollama pull qwen3.6:14b
```
- Size: ~8GB
- RAM needed: 16GB+ recommended
- Quality: Good
- Speed: ~20-30 tokens/sec

### CodeLlama 13B (Specialized for code)
```bash
ollama pull codellama:13b
```
- Size: ~7GB
- RAM needed: 16GB+ recommended
- Quality: Very good for programming
- Speed: ~20-30 tokens/sec

### Llama 3.3 70B (Best quality, requires beefy machine)
```bash
ollama pull llama3.3:70b
```
- Size: ~40GB
- RAM needed: 64GB+ recommended
- Quality: Exceptional
- Speed: ~5-10 tokens/sec

## 🚀 Quick Start

### 1. Pull the model
```bash
ollama pull qwen3.6:27b
```
This downloads the model. **First time takes 10-30 minutes** depending on your internet.

### 2. Test it
```bash
ollama run qwen3.6:27b
```
This opens a chat interface. Try:
```
>>> Review this code: int x = 5 / 0;
```

Type `/bye` to exit.

### 3. Keep Ollama running
Ollama needs to be running as a background service. It usually starts automatically, but if not:

**Windows**: Already running as a service  
**macOS**: Click Ollama icon in menu bar  
**Linux**: 
```bash
systemctl start ollama
```

### 4. Use it in GuidedGrade
1. Open a student folder
2. Check files to analyze (☑)
3. Right-click → "Analyze Selected Files with LLM"
4. Choose **NO** when asked "Azure OpenAI"
5. Wait for feedback (first run is slower, then it's fast)

## ⚙️ Configuration

### Default settings in the app:
- **URL**: `http://localhost:11434`
- **Model**: `qwen3.6:27b`

### To change the model:
In `MainWindow.xaml.cs` line ~542, edit:
```csharp
model: "qwen3.6:27b"  // Change to your preferred model
```

Or we can add a settings UI for this later!

## 💾 System Requirements

| Model | Min RAM | Recommended RAM | Disk Space | Speed |
|-------|---------|-----------------|------------|-------|
| qwen3.6:14b | 16GB | 24GB | 8GB | Fast ⚡ |
| qwen3.6:27b | 24GB | 32GB | 16GB | Medium 🚀 |
| codellama:13b | 16GB | 24GB | 7GB | Fast ⚡⚡ |
| llama3.3:70b | 64GB | 96GB | 40GB | Slow 🐢 |

### Can I use GPU acceleration?
**Yes!** If you have an NVIDIA GPU:
1. Install CUDA toolkit
2. Ollama will automatically use GPU
3. Speed increases **10-50x**
4. RAM requirements drop significantly

## 🔍 How It Works

1. **Student code loaded** from checked files
2. **Anonymized** - only filename + content sent
3. **Prompt built** with requirements + code
4. **Posted to** `http://localhost:11434/api/chat`
5. **Model analyzes** code locally on your machine
6. **Feedback returned** structured as STRENGTHS, ISSUES, SUGGESTIONS, SCORE
7. **Displayed** in popup window

**Zero data leaves your computer!**

## 🆚 Azure OpenAI vs Ollama

| Feature | Azure OpenAI | Ollama |
|---------|-------------|--------|
| **Cost** | ~$0.02-0.05/submission | $0 |
| **Privacy** | Microsoft cloud | 100% local |
| **Speed** | Very fast (cloud GPUs) | Depends on your hardware |
| **Quality** | GPT-4 (best available) | Qwen/Llama (very good) |
| **Setup** | API key needed | Just install |
| **Internet** | Required | Not required |
| **Best for** | High-volume, need GPT-4 | Privacy, cost, offline |

## 🛠️ Troubleshooting

### "Ollama is not running"
```bash
# Check if Ollama is running
curl http://localhost:11434/api/tags

# Should return JSON with available models
```

**Fix**:
- Windows: Restart "Ollama" service
- macOS: Click Ollama icon → Quit → Reopen
- Linux: `systemctl start ollama`

### "Model not found"
```bash
# List installed models
ollama list

# If qwen3.6:27b is missing:
ollama pull qwen3.6:27b
```

### "Out of memory"
Your model is too large for your RAM:
1. Try a smaller model: `ollama pull qwen3.6:14b`
2. Close other programs
3. Restart Ollama

### "Very slow responses"
- **First run**: Model loads into RAM (30-60 seconds)
- **Subsequent runs**: Much faster (already loaded)
- **Consider**: GPU acceleration or smaller model

## 📚 Model Comparison Examples

### Qwen 3.6 27B Output:
```
STRENGTHS:
- Clean variable naming and code structure
- Proper use of const correctness
- Good modularity with separate functions

ISSUES:
- Division by zero on line 15 will cause runtime error
- Missing input validation for negative values
- No error handling for file operations

SUGGESTIONS:
- Add check: if (denominator == 0) before division
- Validate input range at function entry
- Wrap file I/O in try-catch blocks

SCORE: 72/100
```

### CodeLlama 13B Output:
```
STRENGTHS:
- Functions are well-organized
- Consistent indentation style

ISSUES:
- Potential division by zero
- Missing edge case handling
- File errors not caught

SUGGESTIONS:
- Add denominator validation
- Consider using std::optional
- Use RAII for file handling

SCORE: 70/100
```

Both are good! Qwen is slightly more detailed.

## 🔐 Privacy & Security

✅ **Code stays on your machine**  
✅ **No logging to external servers**  
✅ **No student names sent to model**  
✅ **100% FERPA compliant**  
✅ **Works on air-gapped machines**

Perfect for sensitive student data!

## 🚦 Next Steps

1. ✅ Install Ollama
2. ✅ Pull qwen3.6:27b
3. ✅ Test with `ollama run qwen3.6:27b`
4. ✅ Use in app: Right-click → Analyze → Choose "NO" (Ollama)
5. 📋 Compare Azure vs Ollama for your use case
6. ⚙️ (Optional) Add settings UI for model selection

---

**Questions?**
- Ollama Docs: https://github.com/ollama/ollama
- Model Library: https://ollama.ai/library
- Discord: https://discord.gg/ollama
