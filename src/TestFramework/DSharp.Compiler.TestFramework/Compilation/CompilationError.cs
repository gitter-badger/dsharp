﻿namespace DSharp.Compiler.TestFramework.Compilation
{
    public class CompilationError : IError
    {
        public string Message { get; set; }

        public string Location { get; set; }
    }
}