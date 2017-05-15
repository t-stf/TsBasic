// Copyright (c) Thomas Steinfeld. All rights reserved.
// This file is licensed under the Apache License, Version 2.0 
// See the license.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TsBasic
{

	public interface IBasicInputOutput
	{
		void Write(string s);

		void NewLine();

		int PrintZoneWidth { get; }

		void WriteMessage(string s);

		string[] QueryInput(string prompt, string[] varNames);

	}

	class TextWriterIO : IBasicInputOutput
	{
		public int PrintZoneWidth => 16;

		bool atLineStart = true;

		TextWriter tw;

		public TextWriterIO(TextWriter tw)
		{
			this.tw = tw ?? TextWriter.Null;
		}

		public void NewLine()
		{
			tw.WriteLine();
			atLineStart = true;
		}

		public void Write(string s)
		{
			tw.Write(s);
			if (s.Length > 0)
				atLineStart = false;
		}

		public void WriteMessage(string s)
		{
			if (!atLineStart)
				tw.WriteLine();
			tw.WriteLine(s);
			atLineStart = true;
		}

		public virtual string[] QueryInput(string prompt, string[] varNames)
		{
			return null;
		}
	}


	class Context : Dictionary<string, Property>
	{
		public Context() : base(StringComparer.InvariantCultureIgnoreCase)
		{

		}

		public Property GetProperty(string name)
		{
			Property prop;
			this.TryGetValue(name, out prop);
			return prop;
		}

		internal void SetProperty(Property prop)
		{
			this[prop.Name] = prop;
		}

		public void AddToHashset(HashSet<Property> hset, Func<Property, bool> selector)
		{
			foreach (var prop in Values)
			{
				if (selector(prop))
					hset.Add(prop);
			}
		}
	}



}
