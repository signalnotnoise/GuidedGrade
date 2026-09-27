namespace Lab_Feedback_WPF.Services
{
    public static class NativeExitCodes
    {
        public const int StatusDllNotFound = unchecked((int)0xC0000135);
        public const int StatusAccessViolation = unchecked((int)0xC0000005);
        public const int StatusStackBufferOverrun = unchecked((int)0xC0000409);

        public static bool IsDllNotFound(int? exitCode) =>
            exitCode == StatusDllNotFound;

        public static bool IsCrash(int? exitCode)
        {
            if (exitCode == null || exitCode == 0)
                return false;
            if (IsDllNotFound(exitCode))
                return true;

            var unsigned = unchecked((uint)exitCode.Value);
            return unsigned >= 0xC0000000;
        }

        public static string Describe(int? exitCode)
        {
            if (exitCode == null)
                return "n/a";

            var unsigned = unchecked((uint)exitCode.Value);
            return unsigned switch
            {
                0xC0000135 => $"{exitCode} (0xC0000135 STATUS_DLL_NOT_FOUND) — a required DLL was missing beside the executable.",
                0xC0000005 => $"{exitCode} (0xC0000005 STATUS_ACCESS_VIOLATION) — the program crashed.",
                0xC0000409 => $"{exitCode} (0xC0000409 STATUS_STACK_BUFFER_OVERRUN) — the program aborted.",
                _ => exitCode.Value.ToString()
            };
        }
    }
}
