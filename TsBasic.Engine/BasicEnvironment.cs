// Copyright (c) Thomas Steinfeld. All rights reserved.
// This file is licensed under the Apache License, Version 2.0 
// See the license.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TsBasic.Nodes;

namespace TsBasic
{
	public class BasicEnvironment
	{

		const int MaxStackDepth = 500;

		BasicProgram program;

		LineNode[] lines;

		/// <summary>
		/// The data section.
		/// </summary>
		ConstantNode[] data;

		public Errors CompilerErrors;

		Dictionary<string, int> labelMap;

		Context activeContext;

		/// <summary>
		/// Contexts with local variable defs
		/// </summary>
		List<Context> contexts;

		/// <summary>
		/// The program counter -> index in node list.
		/// </summary>
		int pc;

		/// <summary>
		/// The data counter -> index in the data list
		/// </summary>
		int dataCounter;

		Stack<int> pcStack;

		public int Column { get; set; }

		internal int InstructionCount, ExecutedLineCount;

		public IBasicHost Host { get; }

		internal IBasicInputOutput printer => Host.InputOutput;

		public int CurrentLine => pc < lines.Length ? lines[pc].Line : 0;

		public bool WriteCompileRunMessage = true;

		RuntimeErrorNode runtimeError;

		public RuntimeErrorNode LastError => runtimeError;

		public bool HasEnded => runtimeError != null;

		public int ArrayBase;

		public event Action<RuntimeErrorNode> RuntimeErrrorOccurred;

		public BasicEnvironment(IBasicHost host = null)
		{
			this.Host = host ?? new DefaultBasicHost();
			Reinit();
		}

		void Reinit()
		{
			pc = 0;
			Column = 0;
			lines = null;
			InstructionCount = 0;
			ExecutedLineCount = 0;
			ArrayBase = 0;
			labelMap = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
			contexts = new List<Context>();
			activeContext = new Context();
			pcStack = new Stack<int>();
		}

		internal RuntimeErrorNode RuntimeError(RuntimeErrorNode err)
		{
			if (!err.IsFatal || runtimeError == null)
				RuntimeErrrorOccurred?.Invoke(err);
			if (err.IsFatal && runtimeError == null)
				runtimeError = err;

			return err;

		}

		internal RuntimeErrorNode RuntimeError(ERuntimeErrors errNo = ERuntimeErrors.Unspecified, params object[] args)
		{
			var err = new RuntimeErrorNode(CurrentLine, errNo, args);
			return RuntimeError(err);
		}

		internal RuntimeErrorNode RuntimeError(string message)
		{
			return RuntimeError(ERuntimeErrors.UnspecifiedWithInfo, message);
		}

		internal RuntimeErrorNode RuntimeErrorFromException(Exception ex)
		{
			return RuntimeError(new RuntimeErrorNode(CurrentLine, ERuntimeErrors.CsException, ex.ToString()));
		}

		internal Property GetProperty(string name)
		{
			var prop = activeContext.GetProperty(name);
			if (prop != null)
			{
				prop.LastInstruction = InstructionCount;
				return prop;
			}
			for (int i = contexts.Count - 1; i >= 0; i--)
			{
				prop = contexts[i].GetProperty(name);
				if (prop != null)
				{
					prop.LastInstruction = InstructionCount;
					return prop;
				}
			}
			return null;
		}

		internal void SetProperty(Property prop)
		{
			prop.LastInstruction = InstructionCount;
			activeContext.SetProperty(prop);
		}

		internal List<Property> GetProperties(Func<Property, bool> selector)
		{
			var hset = new HashSet<Property>();
			for (int i = 0; i < contexts.Count; i++)
			{
				contexts[i].AddToHashset(hset, selector);
			}
			activeContext.AddToHashset(hset, selector);
			var list = hset.ToList();
			list.Sort((c1, c2) => Math.Sign(c2.LastInstruction - c1.LastInstruction));
			return list;
		}

		public List<string[]> GetPropertyList(int nMax)
		{
			var list = GetProperties((p) => !(p is FunctionProperty));
			nMax = Math.Min(list.Count, nMax);
			var result = new List<string[]>(nMax);
			for (int i = 0; i < nMax; i++)
			{
				var prop = list[i];
				var tup = new string[] { prop.Name, prop.GetValueString(60), prop.GetPropTypeName() };
				result.Add(tup);
			}
			return result;
		}

		internal bool SetDataCounter(int index)
		{
			if (index < 0 || index > data.Length)
				return false;
			dataCounter = index;
			return true;
		}

		void Link()
		{
			for (int i = 0; i < lines.Length; i++)
			{
				var lineNode = lines[i];
				var stmt = lineNode.Statement;
				stmt.Pc = i;
				var defNode = stmt as DefStatementNode;
				if (defNode != null)
				{
					if (!defNode.Link(this))
					{
						CompilerErrors.SemErr(lineNode.Line, 1, $"duplicate def {defNode.name}");
						return;
					}
				}

				var label = lineNode.Label;
				if (label == null)
					continue;
				int oldLine;
				if (labelMap.TryGetValue(label, out oldLine))
				{
					CompilerErrors.SemErr(lineNode.Line, 1, $"duplicate label {label}");
					return;
				}
				labelMap.Add(label, i);
			}
		}

		public BasicNode Run()
		{
			if (lines == null)
				return null;
			Stopwatch watch = Stopwatch.StartNew();
			BasicNode result = null;
			try
			{
				while (true)
				{
					result = Step();
					if (runtimeError != null)
						return runtimeError;
					if (result.IsAbort())
						return result;
				}
			}
			finally
			{
				WriteRunCompleteMessage(watch, result);
			}
		}

		public void WriteRunCompleteMessage(Stopwatch watch, BasicNode result)
		{
			if (WriteCompileRunMessage)
				printer.WriteMessage($"=== {ExecutedLineCount:###,##0} lines with {InstructionCount:###,###,##0} instructions executed in {watch.ElapsedMilliseconds} ms ===");

		}

		public AsyncBasicRunner StartDebug()
		{
			return new AsyncBasicRunner(this);
		}

		internal int NextLineToExecute => pc < lines.Length ? lines[pc].Line : -1;

		internal BasicNode Step()
		{
			if (pc >= lines.Length)
				return ControlNode.Make(EvalResultKind.End, new EndStatementNode());
			var line = lines[pc];
			var result = line.Eval(this);
			ExecutedLineCount++;
			pc++;
			return result;
		}

		public bool LoadAndCompile(string code, string fileNameHint = null)
		{
			Stopwatch watch = Stopwatch.StartNew();
			Reinit();
			Host.InitRun(this);
			var comp = BasicCompiler.Parse(code, fileNameHint);
			this.CompilerErrors = comp.Errors;
			this.program = comp.Program;
			this.lines = program.Lines;
			this.data = program.Data;
			if (CompilerErrors.Count == 0)
				Link();
			if (CompilerErrors.Count > 0)
			{
				comp.DumpErrors((s) => Trace.WriteLine(s));
				return false;
			}

			watch.Stop();
			if (WriteCompileRunMessage)
				printer.WriteMessage($"=== {lines.Length} lines compiled into {program.CalcNodeCount():###,##0} nodes in {watch.ElapsedMilliseconds} ms ===");

			return true;
		}

		public bool LoadAndCompileFile(string fileName)
		{
			var text = File.ReadAllText(fileName);
			return LoadAndCompile(text, fileName);
		}

		internal bool SaveContext()
		{
			if (contexts.Count >= MaxStackDepth)
			{
				RuntimeError(ERuntimeErrors.StackOverflow);
				return false;
			}
			contexts.Add(activeContext);
			activeContext = new Context();
			return true;
		}

		internal void RestoreContext()
		{
			int idx = contexts.Count - 1;
			activeContext = contexts[idx];
			contexts.RemoveAt(idx);
		}

		void PushPc()
		{
			if (pcStack.Count >= MaxStackDepth)
				RuntimeError(ERuntimeErrors.StackOverflow);
			pcStack.Push(pc);
		}

		internal bool Goto(string label, bool isGosub = false)
		{
			int line;
			if (!labelMap.TryGetValue(label, out line))
				return false;
			if (isGosub)
				PushPc();
			pc = line - 1;   // counter will incremented when line is executed
			return true;
		}

		internal void GotoPc(int pc)
		{
			this.pc = pc - 1;  // counter will incremented when line is executed
		}


		internal bool PopPc()
		{
			if (pcStack.Count <= 0)
				return false;
			pc = pcStack.Pop();
			return true;
		}

		internal BasicNode Read(ReferenceNode referenceNode)
		{
			if (dataCounter >= data.Length)
				return RuntimeError(ERuntimeErrors.EndOfData);
			var cons = data[dataCounter++];
			if (referenceNode.IsStringName)
				cons = cons.ToStringConstant();
			else
				cons = cons.ToNumericConstant();
			if (cons == null)
				return RuntimeError(ERuntimeErrors.ConversionError);

			return referenceNode.SetValue(this, cons);
		}


	}



}
