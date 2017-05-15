// Copyright (c) Thomas Steinfeld. All rights reserved.
// This file is licensed under the Apache License, Version 2.0 
// See the license.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TsBasic.Nodes;

namespace TsBasic
{
	class BasicCompiler
	{

		Parser parser;

		Scanner scanner;

		string fileName;

		string code;

		public Errors Errors => parser.errors;

		public BasicProgram Program => parser.prog;

		void Run()
		{
			scanner = Scanner.CreateFromCodeString(code);
			parser = new Parser(scanner);
			parser.Parse();

		}

		public void DumpErrors(Action<string> writelineFunc)
		{
			parser.errors.BuildTraceErrorList(writelineFunc, fileName);
		}

		public static BasicCompiler Parse(string code, string fileNameHint = null)
		{
			if (code.Length == 0 || code[code.Length - 1] != '\n')
				code += "\n";
			var comp = new BasicCompiler()
			{
				code = code,
				fileName = fileNameHint,
			};
			comp.Run();
			comp.Program.CheckProgram();
			return comp;
		}

		public static BasicCompiler ParseFile(string fileName)
		{
			var text = File.ReadAllText(fileName);
			var comp = Parse(text, fileName);
			return comp;
		}

	}
}
