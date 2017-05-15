// Copyright (c) Thomas Steinfeld. All rights reserved.
// This file is licensed under the Apache License, Version 2.0 
// See the license.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsBasic.Nodes;


namespace TsBasic
{

	public interface IBasicHost
	{

		IBasicInputOutput InputOutput { get; }

		void InitRun(BasicEnvironment env);
	}


	public class DefaultBasicHost : IBasicHost
	{

		IBasicInputOutput io;

		Random rand;

		public DefaultBasicHost(IBasicInputOutput io = null)
		{
			this.io = io ?? new TextWriterIO(Console.Out);
		}


		public IBasicInputOutput InputOutput => io;

		void SetInternalFunction(BasicEnvironment env, string name, Func<BasicEnvironment, ConstantNode[], BasicNode> func, params string[] argNames)
		{
			FunctionProperty prop = new FunctionProperty
			{
				Name = name,
				argumentNames = argNames,
				FuncImpl = func
			};
			env.SetProperty(prop);
		}

		void SetIntenalNumFunc(BasicEnvironment env, string name, Func<double, double> func)
		{
			SetInternalFunction(env, name, (e, a) => NumFunc(e, a, func), "x");
		}

		public void InitRun(BasicEnvironment env)
		{
			rand = new Random(1);

			SetIntenalNumFunc(env, "abs", Math.Abs);
			SetIntenalNumFunc(env, "atn", Math.Atan);
			SetIntenalNumFunc(env, "sin", Math.Sin);
			SetIntenalNumFunc(env, "cos", Math.Cos);
			SetIntenalNumFunc(env, "exp", Math.Exp);
			SetIntenalNumFunc(env, "log", Math.Log);
			SetIntenalNumFunc(env, "int", Math.Floor);
			SetIntenalNumFunc(env, "tan", Math.Tan);
			SetIntenalNumFunc(env, "sqr", Math.Sqrt);
			SetIntenalNumFunc(env, "sgn", d => Math.Sign(d));
			SetInternalFunction(env, "rnd", (e, a) => Rnd(e, a));
			SetInternalFunction(env, "randomize", (e, a) => Randomize(e, a));
			SetInternalFunction(env, "=", (e, a) => Compare((cr) => cr == 0, e, a), "x", "y");
			SetInternalFunction(env, "<>", (e, a) => Compare((cr) => cr != 0, e, a), "x", "y");
			SetInternalFunction(env, "<=", (e, a) => Compare((cr) => cr <= 0, e, a), "x", "y");
			SetInternalFunction(env, ">=", (e, a) => Compare((cr) => cr >= 0, e, a), "x", "y");
			SetInternalFunction(env, "<", (e, a) => Compare((cr) => cr < 0, e, a), "x", "y");
			SetInternalFunction(env, ">", (e, a) => Compare((cr) => cr > 0, e, a), "x", "y");
		}

		BasicNode NumFunc(BasicEnvironment env, ConstantNode[] args, Func<double, double> func)
		{
			var num = args[0].ToNumericConstant()?.Value;
			if (!num.HasValue)
				return env.RuntimeError($"internal function called with invalid argument {args[0].ToString()}");
			return new NumericConstantNode(func(num.Value));
		}

		BasicNode Rnd(BasicEnvironment env, ConstantNode[] args)
		{
			return new NumericConstantNode(rand.NextDouble());
		}

		BasicNode Randomize(BasicEnvironment env, ConstantNode[] args)
		{
			rand = new Random();
			return NumericConstantNode.One;
		}

		BasicNode Compare(Func<int, bool> resCheck, BasicEnvironment env, ConstantNode[] args)
		{
			var x = args[0];
			var y = args[1];
			const int InvalidValue = -99;
			int realRes = InvalidValue;
			if (x is NumericConstantNode || y is NumericConstantNode)
			{
				var nx = x.ToNumericConstant()?.Value;
				var ny = y.ToNumericConstant()?.Value;
				if (nx.HasValue && ny.HasValue)
					realRes = nx.Value.CompareTo(ny.Value);
			}
			if (realRes == InvalidValue && (x is StringConstantNode || y is StringConstantNode))
			{
				var sx = x.StringValue;
				var sy = y.StringValue;
				realRes = string.Compare(sx, sy, StringComparison.CurrentCulture);
			}
			if (realRes == InvalidValue && (x is BoolConstantNode || y is BoolConstantNode))
			{
				var bx = x.BoolValue;
				var by = y.BoolValue;
				realRes = bx.CompareTo(by);
			}
			if (realRes == InvalidValue)
				return env.RuntimeError("incompatible types for comparison");
			return new BoolConstantNode(resCheck(realRes));
		}
	}


}
