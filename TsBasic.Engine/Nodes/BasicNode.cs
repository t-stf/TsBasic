// Copyright (c) Thomas Steinfeld. All rights reserved.
// This file is licensed under the Apache License, Version 2.0 
// See the license.txt file in the project root for more information.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace TsBasic.Nodes
{
	public enum EvalResultKind
	{
		Ok,

		Tab,

		Abort,
		Error,
		End,
	}

	public static class NodeExtensions
	{
		public static bool IsAbort(this BasicNode node)
		{
			var cnode = node as ControlNode;
			return cnode?.Kind >= EvalResultKind.Abort;
		}

		public static bool IsError(this BasicNode node)
		{
			var cnode = node as ControlNode;
			return cnode?.Kind == EvalResultKind.Error;
		}


	}

	public class BasicNode
	{

		public virtual BasicNode Eval(BasicEnvironment env)
		{
			env.InstructionCount++;
			return env.RuntimeError(ERuntimeErrors.NotImplemented, $"{this.GetType().Name}.Eval()");
		}

		public override string ToString()
		{
			return $"{this.GetType().Name}";
		}

		public virtual int CalcNodeCount() => 1;

	}

	public class ControlNode : BasicNode
	{
		public BasicNode Target;

		public EvalResultKind Kind;

		public static ControlNode OK = Make(EvalResultKind.Ok, null);

		public static ControlNode Make(EvalResultKind kind, BasicNode node)
		{
			return new ControlNode
			{
				Target = node,
				Kind = kind
			};
		}

		public override string ToString()
		{
			return $"{Kind}: {Target}";
		}

	}

	internal class LineNode : BasicNode
	{
		public int Line => Statement.Line;

		public string Label;

		public StatementNode Statement;

		public LineNode(string label, StatementNode stmt)
		{
			this.Label = label;
			this.Statement = stmt;
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			return Statement.Eval(env);
		}

		public override string ToString()
		{
			return $"{Label}: {Statement}";
		}

		public override int CalcNodeCount() => 1 + Statement.CalcNodeCount();

	}


}
