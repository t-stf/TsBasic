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

	internal class ReferenceNode : ExpressionNode, IEquatable<ReferenceNode>
	{
		static ExpressionNode[] emptyArguments = new ExpressionNode[0];

		public string Name;

		public ExpressionNode[] Arguments;

		internal bool IsStringName;

		static bool CheckStringName(string name)
		{
			return name[name.Length - 1] == '$';
		}

		public ReferenceNode(string func, params ExpressionNode[] args)
		{
			this.Name = func;
			this.Arguments = args.Length == 0 ? emptyArguments : args;
			this.IsStringName = CheckStringName(Name);
		}

		public ReferenceNode(string func, List<ExpressionNode> args) : this(func, args.ToArray())
		{
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(Name);
			if (Arguments.Length > 0)
			{
				sb.Append("(");
				for (int i = 0; i < Arguments.Length; i++)
				{
					if (i > 0)
						sb.Append(", ");
					sb.Append(Arguments[i].ToString());
				}
				sb.Append(")");
			}
			return sb.ToString();
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			env.InstructionCount++;
			return GetValue(env);
		}

		public BasicNode GetValue(BasicEnvironment env)
		{
			var prop = env.GetProperty(Name);
			if (prop == null && Arguments.Length == 0)
				return IsStringName ? ConstantNode.EmptyString : ConstantNode.Zero;
			var consProp = prop as ConstantProperty;
			if (consProp != null)
			{
				if (Arguments.Length > 0)
					return env.RuntimeError($"simple variable '{Name}' is not an array");
				return consProp.Value;
			}
			var funcprop = prop as FunctionProperty;
			if (funcprop != null)
				return ApplyFunc(env, funcprop);
			var arrProp = prop as IArrayProperty;
			if (arrProp != null)
			{
				if (arrProp.DimCount != Arguments.Length)
					return env.RuntimeError(ERuntimeErrors.ArrayDimensionMismatch);
				int[] indexes;
				var result = BuildArrayIndexes(env, out indexes);
				if (result.IsError())
					return result;
				if (IsStringName)
				{
					var sar = arrProp as ArrayProperty<StringConstantNode>;
					StringConstantNode sval;
					if (!sar.GetValue(indexes, out sval))
						return env.RuntimeError(ERuntimeErrors.ArrayIndexOutOfRange, Name);
					return sval ?? ConstantNode.EmptyString;
				}
				else
				{
					var sar = arrProp as ArrayProperty<NumericConstantNode>;
					NumericConstantNode sval;
					if (!sar.GetValue(indexes, out sval))
						return env.RuntimeError(ERuntimeErrors.ArrayIndexOutOfRange, Name);
					return sval ?? ConstantNode.Zero;
				}
			}
			return env.RuntimeError($"Call of undefined function {Name}()");
		}

		public BasicNode BuildArrayIndexes(BasicEnvironment env, out int[] indexes)
		{
			indexes = new int[Arguments.Length];
			for (int i = 0; i < indexes.Length; i++)
			{
				var val = Arguments[i].Eval(env);
				if (val.IsError())
					return val;
				var cval = (val as ConstantNode)?.ToNumericConstant();
				if (cval == null)
					return env.RuntimeError("array index type mismatch");
				indexes[i] = cval.IntValue;
			}
			return this;
		}

		public BasicNode SetValue(BasicEnvironment env, BasicNode value)
		{
			env.InstructionCount++;
			var prop = env.GetProperty(Name);
			if (prop == null)
			{
				if (Arguments.Length == 0)
					prop = new ConstantProperty();
				else
				{
					prop = IsStringName ? (Property)new ArrayProperty<StringConstantNode>(Arguments.Length)
						: new ArrayProperty<NumericConstantNode>(Arguments.Length);
				}
				prop.Name = Name;
				env.SetProperty(prop);
			}

			var consProp = prop as ConstantProperty;
			if (consProp != null)
			{
				if (Arguments.Length > 0)
					return env.RuntimeError($"simple variable '{Name}' is not an array");
				var consValue = value as ConstantNode;
				if (consValue == null)
					return env.RuntimeError();
				consProp.Value = consValue;
				return value;
			}
			var arrProp = prop as IArrayProperty;
			if (Arguments.Length == 0)
				return env.RuntimeError($"array '{Name}' should not be used like a simple variable");
			if (arrProp.DimCount != Arguments.Length)
				return env.RuntimeError(ERuntimeErrors.ArrayDimensionMismatch);
			int[] indexes;
			var result = BuildArrayIndexes(env, out indexes);
			if (result.IsError())
				return result;
			if (IsStringName)
			{
				var sar = arrProp as ArrayProperty<StringConstantNode>;
				var sval = value as StringConstantNode;
				if (sval == null)
					return env.RuntimeError("type mismatch");
				if (!sar.SetValue(indexes, sval))
					return env.RuntimeError(ERuntimeErrors.ArrayIndexOutOfRange, Name);
				return sval;
			}
			else
			{
				var sar = arrProp as ArrayProperty<NumericConstantNode>;
				var sval = value as NumericConstantNode;
				if (sval == null)
					return env.RuntimeError("type mismatch");
				if (!sar.SetValue(indexes, sval))
					return env.RuntimeError(ERuntimeErrors.ArrayIndexOutOfRange,Name);
				return sval;
			}
		}

		internal BasicNode ApplyFunc(BasicEnvironment env, FunctionProperty func)
		{
			env.InstructionCount++;
			if (!env.SaveContext())
				return env.LastError;
			try
			{
				// create the formal parameters with their values
				int nArgs = func.argumentNames.Length;
				ConstantNode[] evaledArgs = new ConstantNode[nArgs];
				for (int i = 0; i < nArgs; i++)
				{
					string argName = func.argumentNames[i];
					BasicNode evArg = null;
					if (i < Arguments.Length)
						evArg = Arguments[i].Eval(env);
					else
						evArg = CheckStringName(argName) ? ConstantNode.EmptyString : ConstantNode.Zero;
					if (evArg.IsAbort())
						return evArg;
					var econs = evArg as ConstantNode;
					if (econs == null)
						env.RuntimeError();
					evaledArgs[i] = econs;
				}
				var result = func.Apply(env, evaledArgs);
				if (result == null)
					result = env.RuntimeError(ERuntimeErrors.NotImplemented, Name);
				return result;
			}
			finally
			{
				env.RestoreContext();
			}
		}

		public bool Equals(ReferenceNode other)
		{
			if (!Name.Equals(other.Name, StringComparison.InvariantCultureIgnoreCase))
				return false;
			return Arguments.Length == other.Arguments.Length;
		}
	}


}
