namespace GitEditor
{
    public struct GitResult
    {
        public int ExitCode;
        public string Output;
        public string Error;

        public bool Success => ExitCode == 0;

        public override string ToString()
        {
            return Success ? Output : $"Error ({ExitCode}): {Error}";
        }
    }
}
