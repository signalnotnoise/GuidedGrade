using System.Text.RegularExpressions;
namespace GuidedGrade.Services;
internal sealed class CodeSection
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public int StartLine { get; set; }
    public int EndLine { get; set; }
}
internal static class CodeSectionDetector
{
    private static readonly Regex Signature = new(@"(?m)^[ \t]*(?:[\w:<>*&]+[ \t]+)*([A-Za-z_][\w:~]*)\s*\([^;{}]*\)\s*(?:const\s*)?(?:noexcept\s*)?\{", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
    private static readonly HashSet<string> Controls = new(StringComparer.Ordinal) { "if","for","while","switch","catch","foreach","using","lock" };
    private static string MaskCommentsAndStrings(string text)
    {
        var masked = text.ToCharArray();
        bool line = false, block = false, quoted = false, escaped = false;
        char quote = '\0';
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i]; var next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (line) { if (c == '\n') line = false; else masked[i] = ' '; continue; }
            if (block) { if (c != '\n') masked[i] = ' '; if (c == '*' && next == '/') { masked[++i] = ' '; block = false; } continue; }
            if (quoted) { if (c != '\n') masked[i] = ' '; if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == quote) quoted = false; continue; }
            if (c == '/' && next is '/' or '*') { line = next == '/'; block = next == '*'; masked[i] = ' '; masked[++i] = ' '; continue; }
            if (c is '\"' or '\'') { quoted = true; quote = c; masked[i] = ' '; }
        }
        return new string(masked);
    }
    internal static List<CodeSection> Extract(string input, string defaultName)
    {
        var text=input.Replace("\r\n","\n"); var result=new List<CodeSection>();
        foreach(Match match in Signature.Matches(MaskCommentsAndStrings(text)))
        {
            var name=match.Groups[1].Value.Split("::").Last(); if(Controls.Contains(name)) continue;
            var brace=match.Index+match.Length-1; var depth=1; var end=brace+1;
            bool quoted=false, escaped=false, lineComment=false, blockComment=false; char quote='\0';
            for(;end<text.Length;end++)
            {
                var c=text[end]; var next=end+1<text.Length?text[end+1]:'\0';
                if(lineComment) { if(c=='\n')lineComment=false; continue; }
                if(blockComment) { if(c=='*'&&next=='/') { blockComment=false;end++; } continue; }
                if(quoted) { if(escaped)escaped=false; else if(c=='\\')escaped=true; else if(c==quote)quoted=false; continue; }
                if(c=='/'&&next=='/') {lineComment=true;end++;continue;}
                if(c=='/'&&next=='*') {blockComment=true;end++;continue;}
                if(c is '\"' or '\'') {quoted=true;quote=c;continue;}
                if(c=='{')depth++; else if(c=='}'&&--depth==0)break;
            }
            if(depth!=0)continue;
            result.Add(new() {Name=name.Length==0?defaultName:name,Code=text[(brace+1)..end],StartLine=1+text[..match.Index].Count(c=>c=='\n'),EndLine=1+text[..end].Count(c=>c=='\n')});
        }
        return result;
    }
}
