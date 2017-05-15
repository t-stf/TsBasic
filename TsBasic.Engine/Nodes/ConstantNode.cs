// Copyright (c) Thomas Steinfeld. All rights reserved.
// This file is licensed under the Apache License, Version 2.0 
// See the license.txt file in the project root for more information.

using System;
using System.Globalization;

namespace TsBasic.Nodes
{
	public abstract class ConstantNode : ExpressionNode
	{

		public static ConstantNode Zero = new NumericConstantNode(0),
			EmptyString = new StringConstantNode(""),
			One = new NumericConstantNode(1)
			;


		public override BasicNode Eval(BasicEnvironment env)
		{
			return this;
		}

		public abstract bool BoolValue { get; }

		public abstract string StringValue { get; }

		internal abstract StringConstantNode ToStringConstant();

		internal abstract NumericConstantNode ToNumericConstant();

		public abstract int Size { get; }
	}


	internal class StringConstantNode : ConstantNode
	{
		public readonly string Text;

		public StringConstantNode()
		{

		}

		public StringConstantNode(string text)
		{
			this.Text = text ?? string.Empty;
		}

		public override bool BoolValue
		{
			get
			{
				var s = Text.ToLower();
				bool result;
				if (bool.TryParse(s, out result))
					return result;
				int iv;
				if (int.TryParse(s, out iv))
					return iv != 0;
				return false;
			}
		}

		public override string StringValue => Text;

		internal override NumericConstantNode ToNumericConstant()
		{
			double val;
			if (!NumericConstantNode.TryParse(Text, out val))
				return null;
			return new NumericConstantNode(val);
		}

		public override string ToString()
		{
			return "\"" + Text + "\"";
		}

		internal override StringConstantNode ToStringConstant()
		{
			return this;
		}

		public override int Size => Text.Length;
	}

	internal class NumericConstantNode : ConstantNode
	{

		public readonly double Value;

		public static bool TryParse(string s, out double d)
		{
			d = 0;
			if (string.IsNullOrEmpty(s))
				return false;
			if (s[0] == '.')
				s = "0" + s;
			return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out d);
		}

		public static string FormatDouble(double d)
		{
			//return string.Format(CultureInfo.InvariantCulture, "{0:g10}", d);
			var s = d.ToString("g", CultureInfo.InvariantCulture);
			s = s.Replace("e", "E");
			return s;
		}

		public NumericConstantNode()
		{
		}

		internal static NumericConstantNode Create(Parser parser, string tokenValue)
		{
			double d;
			if (TryParse(tokenValue, out d))
				return new NumericConstantNode(d);
			parser.SemErr("invalid numeric constant");
			return new NumericConstantNode(double.NaN);
		}

		public NumericConstantNode(double value)
		{
			this.Value = value;
		}

		public override string ToString()
		{
			return FormatDouble(Value);
		}

		internal override StringConstantNode ToStringConstant()
		{
			return new StringConstantNode(ToString());
		}

		internal override NumericConstantNode ToNumericConstant()
		{
			return this;
		}

		public int IntValue => (int)(Math.Floor(Value + 0.5));

		public override bool BoolValue
		{
			get
			{
				if (Value == 0 || double.IsNaN(Value))
					return false;
				return true;
			}
		}

		public override string StringValue => ToString();

		public override int Size => 8;
	}

	class BoolConstantNode : ConstantNode
	{

		bool value;

		public BoolConstantNode(bool value)
		{
			this.value = value;
		}

		public override bool BoolValue => value;

		public override string StringValue => value.ToString();

		internal override NumericConstantNode ToNumericConstant()
		{
			return new NumericConstantNode(value ? 1 : 0);
		}

		internal override StringConstantNode ToStringConstant()
		{
			return new StringConstantNode(StringValue);
		}

		public override int Size => 1;
	}



}
