// Copyright (c) Thomas Steinfeld. All rights reserved.
// This file is licensed under the Apache License, Version 2.0 
// See the license.txt file in the project root for more information.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using TsBasic.Nodes;

namespace TsBasic
{

	internal class BasicProgram
	{

		List<LineNode> linesList;

		Stack<ForStatementNode> openFors;

		List<ConstantNode> data;

		Parser parser;

		public LineNode[] Lines => linesList.ToArray();

		public ConstantNode[] Data => data.ToArray();

		public BasicProgram(Parser parser)
		{
			linesList = new List<LineNode>();
			openFors = new Stack<ForStatementNode>();
			data = new List<ConstantNode>();
			this.parser = parser;
		}

		internal void AddLine(LineNode line)
		{
			linesList.Add(line);
		}

		public void AddData(ConstantNode cons)
		{
			data.Add(cons);
		}

		public void AddData(IList<ConstantNode> nodes)
		{
			data.AddRange(nodes);
		}

		public int CalcNodeCount()
		{
			int sum = 0;
			for (int i = 0; i < linesList.Count; i++)
			{
				sum += linesList[i].CalcNodeCount();
			}
			return sum;
		}
		
		internal void CheckForVarName(ExpressionNode varName)
		{
			ReferenceNode refNode = varName as ReferenceNode;
			foreach (var fnode in openFors)
			{
				if (fnode.Var.Equals(refNode))
				{
					parser.SemErr($"FOR variable {refNode.Name} was already used in line {fnode.Line}");
				}
			}

		}

		public void PushFor(ForStatementNode forNode)
		{
			openFors.Push(forNode);
		}

		public bool PopNext(ref NextStatementNode next)
		{
			if (openFors.Count == 0)
			{
				parser.SemErr("NEXT without FOR");
				return false;
			}
			var forNode = openFors.Pop();
			if (next.Var == null)
				next = new NextStatementNode(forNode.Var);
			else if (!forNode.Var.Equals(next.Var))
			{
				parser.SemErr($"different var names in FOR ({forNode.Var.Name}) and NEXT ({next.Var.Name})");
				return false;
			}
			next.For = forNode;
			forNode.nextNode = next;
			return true;
		}

		public bool CheckProgram()
		{
			// if we have already errors, no need to produce more.
			if (parser.errors.Count > 0)
				return true;
			bool result = true;
			if (openFors.Count>0)
			{
				result = false;
				foreach (var node in openFors)
				{
					parser.SemErr("FOR without NEXT", node.Line, node.Col);
				}
			}
			return result;
		}


	}

}
