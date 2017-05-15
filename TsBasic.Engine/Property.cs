// Copyright (c) Thomas Steinfeld. All rights reserved.
// This file is licensed under the Apache License, Version 2.0 
// See the license.txt file in the project root for more information.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using TsBasic.Nodes;

namespace TsBasic
{

	abstract class Property : IEqualityComparer<Property>
	{
		public string Name;

		/// <summary>
		/// The last instruction this property was accessed.
		/// </summary>
		internal int LastInstruction;

		public bool Equals(Property x, Property y)
		{
			return StringComparer.InvariantCultureIgnoreCase.Equals(x.Name, y.Name);
		}

		public override int GetHashCode()
		{
			return GetHashCode(this);
		}

		public int GetHashCode(Property obj)
		{
			return StringComparer.InvariantCultureIgnoreCase.GetHashCode(obj.Name);
		}

		public virtual string GetValueString(int maxCharsHint = 60)
		{
			return "abstract propery";
		}

		public override string ToString()
		{
			return GetValueString(1000);
		}

		public virtual string GetPropTypeName()
		{
			return "unknown type";
		}

		public abstract int Size { get; }
	}

	class ConstantProperty : Property
	{
		public ConstantNode Value;

		public override string GetValueString(int maxCharsHint = 60)
		{
			return Value.ToString();
		}

		public override string GetPropTypeName()
		{
			return Value is NumericConstantNode ? "number" : "string";
		}

		public override int Size => Value?.Size ?? 0;

	}

	class FunctionProperty : Property
	{
		public string[] argumentNames;

		public Func<BasicEnvironment, ConstantNode[], BasicNode> FuncImpl;

		public virtual BasicNode Apply(BasicEnvironment env, ConstantNode[] args)
		{
			return FuncImpl?.Invoke(env, args);
		}

		public override int Size => 0;
	}

	interface IArrayProperty
	{
		int DimCount { get; }

		BasicNode Redim(BasicEnvironment env, int[] newDims);
	}

	class ArrayProperty<T> : Property, IArrayProperty where T : ConstantNode, new()
	{
		int[] dims;

		T[] data;

		int internalSize;

		int totalLength;

		private int GetTotalArrayLength(int[] dims)
		{
			int prod = 1;
			for (int i = 0; i < dims.Length; i++)
			{
				prod *= dims[i];
			}
			return prod;
		}

		public int DimCount => dims.Length;

		public ArrayProperty(int[] dims)
		{
			this.dims = dims;
			data = new T[totalLength = GetTotalArrayLength(dims)];
		}

		public ArrayProperty(int dimCount)
		{
			dims = new int[dimCount];
			for (int i = 0; i < dimCount; i++)
			{
				dims[i] = 11;
			}
			data = new T[totalLength = GetTotalArrayLength(dims)];
		}

		int CheckIndex(int[] indexes)
		{
			int sum = 0;
			int prod = 1;
			for (int i = 0; i < dims.Length; i++)
			{
				var dimi = dims[i];
				var indi = indexes[i];
				if (indi < 0 || indi >= dimi)
					return -1;
				sum += prod * indi;
				prod *= dimi;
			}
			return sum;
		}

		internal bool SetValue(int[] indexes, T val)
		{
			int index = CheckIndex(indexes);
			if (index < 0)
				return false;
			data[index] = val;
			return true;
		}

		internal bool GetValue(int[] indexes, out T val)
		{
			val = null;
			int index = CheckIndex(indexes);
			if (index < 0)
				return false;
			var oldValue = data[index];
			if (oldValue != null)
				internalSize -= oldValue.Size;
			val = data[index];
			internalSize += oldValue.Size;
			return true;

		}

		public override string GetValueString(int maxCharsHint)
		{
			StringBuilder sb = new StringBuilder(maxCharsHint + 10);
			sb.Append("(");
			for (int i = 0; i < data.Length; i++)
			{
				if (i > 0)
					sb.Append(", ");
				var val = data[i];
				if (val != null)
					sb.Append(val.ToString());
				if (sb.Length >= maxCharsHint)
				{
					sb.Append(",...");
					break;
				}
			}
			sb.Append(")");
			return sb.ToString();
		}

		public override string GetPropTypeName()
		{
			var inds = string.Join(",", dims.Select(iv => iv.ToString()).ToArray());
			var tname = (new ConstantProperty() { Value = new T() }).GetPropTypeName();
			return $"array({inds}) of {tname}";
		}

		public BasicNode Redim(BasicEnvironment env, int[] newDims)
		{
			if (newDims.Length != dims.Length)
				return env.RuntimeError("dimension count mismatch");
			bool sameDims = true;
			for (int i = 0; i < newDims.Length; i++)
			{
				if (dims[i] == newDims[i])
					continue;
				sameDims = false;
				break;
			}
			if (sameDims)
				return ControlNode.Make(EvalResultKind.Ok, null);

			int[] curIndex = new int[dims.Length];
			var dst = new ArrayProperty<T>(newDims);
			while (true)
			{
				curIndex[0]++;
				for (int i = 0; i < curIndex.Length - 1; i++)
				{
					if (curIndex[i] < dims[i])
						break;
					if (i == curIndex.Length - 1)
						break;
					curIndex[i] = 0;
					curIndex[i + 1]++;
				}
				if (curIndex[curIndex.Length - 1] >= dims[curIndex.Length - 1])
					break;
				T val;
				if (GetValue(curIndex, out val) && val != null)
					dst.SetValue(curIndex, val);
			}

			return ControlNode.Make(EvalResultKind.Ok, null);
		}

		public override int Size => internalSize + totalLength * 4;

	}


}
