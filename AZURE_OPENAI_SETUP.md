# Azure OpenAI Integration - Phase 1

## ✅ What's Implemented

### 1. File Selection System
- **Checkboxes appear next to files** (not directories) in the tree view
- Teachers can check/uncheck files to include in analysis
- Only selected files will be sent to Azure OpenAI

### 2. Azure OpenAI Service
- `AzureOpenAIService.cs` - Handles API communication
- Anonymizes file paths (only sends filenames + content, no student info)
- Structured prompts for code review
- Returns formatted feedback: STRENGTHS, ISSUES, SUGGESTIONS, SCORE

### 3. UI Integration
- **Right-click context menu** in file tree
- "Analyze Selected Files with LLM" menu item
- Feedback displayed in popup window

## 🔧 Configuration Required

Before using, you need to configure Azure OpenAI settings:

### Step 1: Get Azure OpenAI Credentials
1. Go to Azure Portal → Your OpenAI resource
2. Copy these values:
   - **Endpoint**: `https://your-resource.openai.azure.com/`
   - **API Key**: From "Keys and Endpoint" section
   - **Deployment Name**: Name you gave your GPT-4 deployment

### Step 2: Update Code (Temporary)
In `MainWindow.xaml.cs`, line ~499, replace:
```csharp
var endpoint = "YOUR_AZURE_ENDPOINT";
var apiKey = "YOUR_API_KEY";
var deployment = "YOUR_DEPLOYMENT_NAME";
```
with your actual values.

## 📋 How to Use

1. **Open student folder** (File → Open Folder)
2. **Select a student** from the list
3. **Check files** you want analyzed (e.g., ☑ main.cpp, ☑ student.h)
4. **Right-click** in the tree view
5. **Click** "Analyze Selected Files with LLM"
6. **Review feedback** in popup window

## 🔒 Privacy Design

✅ **Student names/IDs never sent** to Azure OpenAI  
✅ **File paths anonymized** (only filenames sent)  
✅ **Teacher controls** which files are analyzed  
✅ **FERPA compliant** when using Azure OpenAI (data stays in your tenant)

## 📝 Prompt Structure

The system sends to Azure OpenAI:
```
# ASSIGNMENT REQUIREMENTS
[Your lab requirements - TODO: configure this]

# SUBMITTED CODE
=== main.cpp ===
[file contents]

=== student.h ===
[file contents]

# TASK
Analyze the code against requirements and provide:
1. STRENGTHS: What was done well
2. ISSUES: Problems, bugs, missing requirements
3. SUGGESTIONS: Specific improvements
4. SCORE: Overall score out of 100
```

## 🚧 TODO / Next Steps

### Phase 2: Configuration UI
- [ ] Settings window for Azure credentials
- [ ] Save settings securely (encrypted)
- [ ] Requirements library/template system
- [ ] Test different deployment names/models

### Phase 3: Requirements Management
- [ ] Auto-detect `requirements.txt` or `README.md` in student folders
- [ ] Library of reusable lab requirements
- [ ] Manual paste requirements option
- [ ] Point value mapping

### Phase 4: Feedback Integration
- [ ] Parse LLM score into grading form
- [ ] Save feedback to student record
- [ ] Batch analysis (multiple students)
- [ ] Feedback templates

### Phase 5: Execution & Testing
- [ ] Launch student programs
- [ ] Capture console I/O
- [ ] LLM-driven test inputs
- [ ] Record transcript for review

## 🔑 Sample Configuration

Example Azure OpenAI setup:
```
Endpoint: https://myschool-cs-grading.openai.azure.com/
Deployment: gpt-4o-2024-11-01
Model: GPT-4o
Cost: ~$0.02-0.05 per submission
```

## 📊 Expected Feedback Format

```
STRENGTHS:
- Clean code organization with proper header files
- Good use of const correctness
- Efficient algorithms used

ISSUES:
- Missing edge case handling for empty input
- Memory leak in destructor
- Does not meet requirement #3 (error handling)

SUGGESTIONS:
- Add input validation before processing
- Implement copy constructor and assignment operator
- Consider using smart pointers instead of raw pointers

SCORE: 78/100
```

## 🐛 Troubleshooting

**"Azure OpenAI is not configured"**
→ Update the endpoint, API key, and deployment name in code

**"No files selected"**
→ Check the checkboxes next to files you want to analyze

**Empty or error response**
→ Check deployment name matches your Azure resource
→ Verify API key is correct
→ Check Azure OpenAI quota/limits

---

**Questions?** Check Azure OpenAI documentation:
https://learn.microsoft.com/en-us/azure/ai-services/openai/
