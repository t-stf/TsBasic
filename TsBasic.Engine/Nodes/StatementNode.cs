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

	internal class StatementNode : BasicNode
	{
		public int Line;

		public int Col;

		public int Pc;
	}


	internal class EndStatementNode : StatementNode
	{
		public override BasicNode Eval(BasicEnvironment env)
		{
			return ControlNode.Make(EvalResultKind.End, this);
		}

		public override string ToString()
		{
			return "END";
		}

	}

	internal class RemStatementNode : StatementNode
	{
		public readonly string Content;

		public RemStatementNode(string content)
		{
			this.Content = content;
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			return this;
		}

		public override string ToString()
		{
			return $"REM {Content}";
		}
	}


	internal class LetStatementNode : StatementNode
	{
		public readonly ReferenceNode Variable;

		public readonly ExpressionNode Expression;

		public LetStatementNode(ReferenceNode var, ExpressionNode expr)
		{
			this.Variable = var;
			this.Expression = expr;
		}

		public override string ToString()
		{
			return $"LET {Variable} = {Expression}";
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			env.InstructionCount++;
			var evaledRhs = Expression.Eval(env) as ConstantNode;
			if (evaledRhs == null)
				return env.RuntimeError();
			return Variable.SetValue(env, evaledRhs);
		}

		public override int CalcNodeCount() => Variable.CalcNodeCount() + Expression.CalcNodeCount();

	}


	class JumpStatementNode : StatementNode
	{
		public readonly string Label;

		public readonly bool IsGosub;

		public JumpStatementNode(string label, bool isGosub = false)
		{
			this.Label = label;
			this.IsGosub = isGosub;
		}

		public override string ToString()
		{
			return (IsGosub ? "GOSUB " : "GOTO ") + Label;
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			env.InstructionCount++;
			if (!env.Goto(Label, IsGosub))
				return env.RuntimeError(ERuntimeErrors.JumpToUndefinedLabel, Label);
			return this;
		}

		public override int CalcNodeCount() => 2;

	}


	class ReturnStatementNode : StatementNode
	{
		public override string ToString()
		{
			return "RETURN";
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			env.InstructionCount++;
			if (!env.PopPc())
				return env.RuntimeError(ERuntimeErrors.ReturnWithoutGosub);
			return this;
		}


	}

	class IfThenStatementNode : StatementNode
	{
		ExpressionNode boolExpr;

		string thenLabel;

		public IfThenStatementNode(ExpressionNode boolExpr, string thenLabel)
		{
			this.boolExpr = boolExpr;
			this.thenLabel = thenLabel;
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			env.InstructionCount++;
			var node = boolExpr.Eval(env);
			if (node.IsAbort())
				return node;
			var cnode = node as ConstantNode;
			if (cnode == null)
				env.RuntimeError();
			bool b = cnode.BoolValue;
			if (!b)
				return this;

			b = env.Goto(thenLabel);
			if (!b)
				return env.RuntimeError(ERuntimeErrors.JumpToUndefinedLabel, thenLabel);
			return this;

		}

		public override int CalcNodeCount() => 1 + boolExpr.CalcNodeCount();

	}

	abstract class ForNextStatementBase : StatementNode
	{
		ReferenceNode varExpr;

		public ReferenceNode Var => varExpr;

		public ForNextStatementBase(ExpressionNode var)
		{
			this.varExpr = var as ReferenceNode;
		}
	}

	class ForStatementNode : ForNextStatementBase
	{

		internal NextStatementNode nextNode;

		ExpressionNode from, to, step;

		internal NumericConstantNode own1, own2;

		public ForStatementNode(ExpressionNode var, ExpressionNode from, ExpressionNode to, ExpressionNode step) : base(var)
		{
			this.from = from;
			this.to = to;
			this.step = step;
		}

		public override int CalcNodeCount() => 1 + from.CalcNodeCount() + to.CalcNodeCount() + step.CalcNodeCount();


		public override BasicNode Eval(BasicEnvironment env)
		{
			env.InstructionCount++;
			own1 = to.Eval(env) as NumericConstantNode;
			own2 = step.Eval(env) as NumericConstantNode;
			if (own1 == null || own2 == null)
				return env.RuntimeError();
			//if (own2.Value.IsZero)
			//	return env.RuntimeError(ERuntimeErrors.ZeroStep);
			var initVal = from.Eval(env);
			var setRes = Var.SetValue(env, initVal);
			if (env.HasEnded)
				return this;

			LoopStep(env, initVal as NumericConstantNode);
			return this;
		}

		internal bool LoopStep(BasicEnvironment env, NumericConstantNode v)
		{
			var cmp = v.Value.CompareTo(own1.Value);
			if (cmp * Math.Sign(own2.Value) > 0)
			{
				env.GotoPc(nextNode.Pc + 1);  // jump out of loop
				own1 = own2 = null;
				return false;
			}
			return true;
		}

	}

	class NextStatementNode : ForNextStatementBase
	{

		// for that belongs to this next
		internal ForStatementNode For;

		public NextStatementNode(ExpressionNode var) : base(var)
		{
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			env.InstructionCount++;
			if (For.own1 == null || For.own2 == null)
				return env.RuntimeError(ERuntimeErrors.JumpIntoForLoop);
			env.InstructionCount++;
			var v = Var.GetValue(env) as NumericConstantNode;
			if (v == null)
				return env.RuntimeError();
			var val = v.Value;
			val += For.own2.Value;
			var valNode = new NumericConstantNode(val);
			Var.SetValue(env, valNode);
			if (For.LoopStep(env, valNode))
				env.GotoPc(For.Pc + 1);

			return this;
		}

	}

	class ReadStatementNode : StatementNode
	{

		ReferenceNode[] vars;

		public ReadStatementNode(List<ReferenceNode> vars)
		{
			this.vars = vars.ToArray();
		}

		public override int CalcNodeCount() => vars.Length;

		public override BasicNode Eval(BasicEnvironment env)
		{
			env.InstructionCount += vars.Length;
			for (int i = 0; i < vars.Length; i++)
			{
				var result = env.Read(vars[i]);
				if (result.IsError())
					return result;
			}
			return this;
		}
	}

	class RestoreStatementNode : StatementNode
	{
		public override BasicNode Eval(BasicEnvironment env)
		{
			env.SetDataCounter(0);
			return this;
		}
	}

	class DimStatementNode : StatementNode
	{
		ExpressionNode[] dimension;

		ReferenceNode varName;

		public DimStatementNode(ExpressionNode var, List<ExpressionNode> list)
		{
			dimension = list.ToArray();
			varName = (var as ReferenceNode);
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			env.InstructionCount++;
			var prop = env.GetProperty(varName.Name);
			varName.Arguments = dimension;
			int[] indexes;
			var result = varName.BuildArrayIndexes(env, out indexes);
			if (result.IsError())
				return result;
			for (int i = 0; i < indexes.Length; i++)
			{
				indexes[i]++;
				if (indexes[i] <= 0)
					return env.RuntimeError($"invalid array dimension {indexes[i]}");
			}
			// new array
			if (prop == null)
			{
				prop = varName.IsStringName ? (Property)new ArrayProperty<StringConstantNode>(indexes) : new ArrayProperty<NumericConstantNode>(indexes);
				prop.Name = varName.Name;
				env.SetProperty(prop);
				return this;
			}
			// change existing array
			var arrProp = prop as IArrayProperty;
			if (arrProp == null)
				return env.RuntimeError($"DIM of non array variable {varName}");
			return arrProp.Redim(env, indexes);
		}

	}

	class StatementSequenceNode : StatementNode
	{

		StatementNode[] statements;

		public StatementSequenceNode(List<StatementNode> statements)
		{
			this.statements = statements.ToArray();
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			BasicNode result = null;
			for (int i = 0; i < statements.Length; i++)
			{
				result = statements[i].Eval(env);
				if (result.IsError())
					return result;
			}
			return result;
		}
	}

	class OptionStatement : StatementNode
	{
		string optionName;

		string value;

		public OptionStatement(string name, string value)
		{
			this.optionName = name;
			this.value = value;
		}


		public override BasicNode Eval(BasicEnvironment env)
		{
			env.ArrayBase = int.Parse(value);
			return this;
		}
	}

	class OnGotoStatementNode : StatementNode
	{
		ExpressionNode expr;

		string[] labels;

		public OnGotoStatementNode(ExpressionNode node, List<string> labelList)
		{
			this.expr = node;
			this.labels = labelList.ToArray();
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			env.InstructionCount++;
			var cons = expr.Eval(env) as NumericConstantNode;
			if (cons == null)
				return env.RuntimeError("Expression did not evaluate to a numeric constant");
			int index = cons.IntValue - 1;
			if (index < 0 || index >= labels.Length)
				return env.RuntimeError(ERuntimeErrors.OnGotoIndexInvalid, index + 1);
			if (!env.Goto(labels[index], false))
				return env.RuntimeError(ERuntimeErrors.JumpToUndefinedLabel, labels[index]);
			return this;
		}

	}

	class NopStatementNode : StatementNode
	{

		public override BasicNode Eval(BasicEnvironment env)
		{
			return this;
		}
	}

	class SimpleStatementNode : StatementNode
	{
		string cmdName;

		ReferenceNode refNode;

		public SimpleStatementNode(string name)
		{
			this.cmdName = name;
			refNode = new ReferenceNode(cmdName);
		}

		public override string ToString()
		{
			return cmdName;
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			return refNode.Eval(env);
		}
	}

	class DefStatementNode : StatementNode
	{
		internal string name;

		string[] parNames;

		ExpressionNode expr;

		int argCount => parNames.Length;

		public DefStatementNode(string name, List<string> parNames, ExpressionNode expr)
		{
			this.name = name;
			this.parNames = parNames.ToArray();
			this.expr = expr;
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			return this;
		}

		BasicNode Call(BasicEnvironment env, ConstantNode[] args)
		{
			if (!env.SaveContext())
				return env.LastError;
			if (argCount != args.Length)
				return env.RuntimeError("Argument count mismatch");
			for (int i = 0; i < args.Length; i++)
			{
				var prop = new ConstantProperty()
				{
					Value = args[i],
					Name = parNames[i],
				};
				env.SetProperty(prop);
			}
			var result = expr.Eval(env);
			env.RestoreContext();
			return result;
		}

		internal bool Link(BasicEnvironment env)
		{
			if (env.GetProperty(name) != null)
				return false;
			var f = new FunctionProperty
			{
				argumentNames = parNames,
				FuncImpl = Call,
				Name = name
			};
			env.SetProperty(f);
			return true;
		}
	}

	class InputStatementNode : StatementNode
	{
		string prompt;

		ReferenceNode[] refNames;

		string[] varNames;

		public InputStatementNode(string prompt, IList<ReferenceNode> references)
		{
			this.prompt = prompt ?? string.Empty;
			this.refNames = references.ToArray();
			varNames = refNames.Select(r => r.Name).ToArray();
		}

		public override BasicNode Eval(BasicEnvironment env)
		{
			var res = env.Host.InputOutput.QueryInput(prompt, varNames);
			if (res == null)
				return env.RuntimeError("User aborted program during input");
			if (res.Length < varNames.Length)
				return env.RuntimeError($"INPUT expected {varNames.Length} values but got only {res.Length}");
			for (int i = 0; i < refNames.Length; i++)
			{
				env.InstructionCount++;
				var varNode = refNames[i];
				ConstantNode consNode = Parser.ParseDatum(res[i]);
				if (varNode.IsStringName)
					consNode= consNode?.ToStringConstant();
				else
					consNode = consNode?.ToNumericConstant();
				if (consNode == null)
					return env.RuntimeError($"INPUT variable ({refNames[i]}) conversion error from '{res[i]}'");
				varNode.SetValue(env, consNode);
			}
			return this;
		}

	}

}
