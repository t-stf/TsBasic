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

	public class ExpressionNode : BasicNode
	{

	}

	internal class FuncExpressionNode : ExpressionNode
	{
		public readonly string Functor;

		public readonly ExpressionNode[] Arguments;

		public FuncExpressionNode(string func, params ExpressionNode[] args)
		{
			this.Functor = func.ToUpper();
			this.Arguments = args;
		}

		public FuncExpressionNode(string func, List<ExpressionNode> args) : this(func, args.ToArray())
		{
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			env.InstructionCount++;
			ExpressionNode[] evaledArgs = new ExpressionNode[Arguments.Length];
			for (int i = 0; i < Arguments.Length; i++)
			{
				var evArg = Arguments[i].Eval(env);
				if (evArg.IsAbort())
					return evArg;
				evaledArgs[i] = evArg as ConstantNode;
			}
			BasicNode result;
			switch (Functor)
			{
				case "TAB":
					result = ControlNode.Make(EvalResultKind.Tab, evaledArgs[0]);
					break;
				default: result = env.RuntimeError(ERuntimeErrors.CallToUnknownFunction, Functor); break;
			}
			return result;
		}
	}
	internal class OpSequenceExpressionNode : ExpressionNode
	{
		protected object[] sequence;

		public OpSequenceExpressionNode(List<object> sequence)
		{
			this.sequence = sequence.ToArray();
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < sequence.Length; i++)
			{
				if (i > 0)
					sb.Append(' ');
				sb.Append(sequence[i].ToString());
			}
			return sb.ToString();
		}

		public NumericConstantNode EvalAtIndex(int index, BasicEnvironment env)
		{
			var arg = sequence[index] as BasicNode;
			if (arg == null)
				return null;
			arg = arg.Eval(env);
			var ncn = arg as NumericConstantNode;
			if (ncn != null)
				return ncn;
			var cnode = arg as ConstantNode;
			if (cnode != null)
				ncn = cnode.ToNumericConstant();
			if (ncn == null)
				env.RuntimeError(ERuntimeErrors.ConversionError);
			return ncn;
		}
	}

	internal class AddSequenceExpressionNode : OpSequenceExpressionNode
	{

		public AddSequenceExpressionNode(List<object> sequence) : base(sequence)
		{
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			double sum = 0;
			bool opMinus = false;
			for (int i = 0; i < sequence.Length; i++)
			{
				object item = sequence[i];
				string op = item as string;
				if (op != null)
				{
					opMinus = op == "-";
					continue;
				}
				var arg = EvalAtIndex(i, env);
				if (arg == null)
					return env.RuntimeError();
				var argVal = (arg).Value;
				env.InstructionCount++;
				if (opMinus)
					argVal = -argVal;
				if (sum != 0)
					sum += argVal;
				else
					sum = argVal;
			}
			return new NumericConstantNode(sum);

		}
	}

	internal class MulSequenceExpressionNode : OpSequenceExpressionNode
	{

		public const int MulComplexity = 3, DivComplexity = 5;

		public MulSequenceExpressionNode(List<object> sequence) : base(sequence)
		{
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			double prod = 1;
			bool opDiv = false;
			for (int i = 0; i < sequence.Length; i++)
			{
				object item = sequence[i];
				string op = item as string;
				if (op != null)
				{
					opDiv = op == "/";
					continue;
				}
				var arg = EvalAtIndex(i, env);
				if (arg == null)
					return env.RuntimeError();
				var argVal = (arg).Value;
				env.InstructionCount += opDiv ? DivComplexity : MulComplexity;
				if (opDiv)
					prod /= argVal;
				else
					prod *= argVal;
			}
			return new NumericConstantNode(prod);
		}
	}


	class PowSequenceExpressionNode : OpSequenceExpressionNode
	{

		public PowSequenceExpressionNode(List<object> sequence) : base(sequence)
		{

		}

		double IntPow(double x, int n, BasicEnvironment env)
		{
			bool invert = false;
			if (n < 0)
			{
				n = -n;
				invert = true;
			}
			double res = 1.0;
			double prod = x;
			while (n > 0)
			{
				if ((n & 1) != 0)
				{
					env.InstructionCount += MulSequenceExpressionNode.MulComplexity;
					res *= prod;
				}
				env.InstructionCount += MulSequenceExpressionNode.MulComplexity;
				prod = prod * prod;
				n >>= 1;
			}
			if (invert)
				return 1.0 / res;
			return res;
		}


		public override BasicNode Eval(BasicEnvironment env)
		{
			var arg = EvalAtIndex(0, env);
			if (arg == null)
				return env.RuntimeError();
			double prod = arg.Value;
			for (int i = 1; i < sequence.Length; i++)
			{
				object item = sequence[i];
				arg = EvalAtIndex(i, env);
				if (arg == null)
					return env.RuntimeError();
				var num = arg.Value;
				var iv = arg.IntValue;
				var back = (double)iv;
				if (back == num)
				{
					prod = IntPow(prod, iv, env);
					continue;
				}
				//if (num < 0)
				//	return env.RuntimeError("negative exponent not supported");
				prod = Math.Pow(prod, num);
			}
			return new NumericConstantNode(prod);
		}

	}


}
