using System;
using System.Collections.Generic;
using System.Text;

namespace GuidedGrade.Models
{
    public class Violation
    {
        public int LineNumber { get; init; }
        public string MatchedText { get; init; }
        public string Context { get; init; }
        public string LineContent { get; init; }

        public Violation(int lineNumber, string matchedText, string context, string lineContent)
        {
            LineNumber = lineNumber;
            MatchedText = matchedText;
            Context = context;
            LineContent = lineContent;
        }
    }
}