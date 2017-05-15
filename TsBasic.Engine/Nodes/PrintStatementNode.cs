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

	internal class PrintStatementNode : StatementNode
	{

		public List<object> items = new List<object>();

		IBasicInputOutput printer;

		BasicEnvironment runtime;

		void WriteString(string s)
		{
			printer.Write(s);
			runtime.Column += s.Length;
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			env.InstructionCount++;
			runtime = env;
			printer = env.printer;
			bool suppressNL = false;
			for (int i = 0; i < items.Count; i++)
			{
				suppressNL = false;
				var item = items[i];
				var node = item as BasicNode;
				if (node != null)
				{
					node = node.Eval(env);
					if (node.IsAbort())
						return node;
					var sNode = node as StringConstantNode;
					if (sNode != null)
					{
						WriteString(sNode.Text);
						continue;
					}
					var nNode = node as NumericConstantNode;
					if (nNode != null)
					{
						WriteNumericConstant(nNode);
						continue;
					}
					var cNode = node as ControlNode;
					if (cNode.Kind == EvalResultKind.Tab)
					{
						var numNode = cNode.Target as NumericConstantNode;
						int amount = numNode.IntValue;
						if (amount <= 0)
						{
							env.RuntimeError(ERuntimeErrors.InvalidTab, amount);
							amount = 1;
						}
						amount--;
						if (runtime.Column < amount)
							WriteString(new string(' ', amount - runtime.Column));
						else
						{
							printer.NewLine();
							WriteString(new string(' ', amount));
						}
						continue;
					}
					return env.RuntimeError();
				}
				var sval = item as string;
				if (sval == ",")
				{
					int rem = runtime.Column % printer.PrintZoneWidth;
					rem = printer.PrintZoneWidth - rem;
					WriteString(new string(' ', rem));
				}
				if (sval == ";" || sval == ",")
				{
					suppressNL = true;
					continue;
				}
				return env.RuntimeError();
			}
			if (!suppressNL)
			{
				runtime.Column = 0;
				printer.NewLine();
			}
			return this;
		}

		private void WriteNumericConstant(NumericConstantNode nu)
		{
			var value = nu.Value;
			if (double.IsNaN(value) || double.IsInfinity(value))
			{
				WriteString(nu.ToString());
				return;
			}
			if (Math.Sign(value) >= 0)
				WriteString(" ");
			WriteString(nu.ToString());
			WriteString(" ");
		}

		public override int CalcNodeCount()
		{
			int sum = 0;
			for (int i = 0; i < items.Count; i++)
			{
				var node = items[i] as BasicNode;
				sum += node?.CalcNodeCount() ?? 1;
			}
			return 1 + sum;
		}

	}

}
