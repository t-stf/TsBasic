// Copyright (c) Thomas Steinfeld. All rights reserved.
// This file is licensed under the Apache License, Version 2.0 
// See the license.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TsBasic.Nodes;

namespace TsBasic
{

	internal partial class Scanner
	{
		const char EOL = '\n';
		const int eofSym = 0; /* pdt */
		public IBuffer buffer; // scanner buffer

		Token t;          // current token
		int ch;           // current input character
		int pos;          // byte position of current character
		int charPos;      // position by unicode characters starting with 0
		int col;          // column number of current character
		int line;         // line number of current character
		int oldEols;      // EOLs that appeared in a comment;
		static readonly Dictionary<int, int> start; // maps first token character to start state

		Token tokens;     // list of tokens already peeked (first token is a dummy)
		Token pt;         // current peek token

		char[] tval = new char[128]; // text of current token
		int tlen;         // length of current token

		public Scanner(string fileName)
		{
			try
			{
				Stream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
				buffer = new Buffer(stream, false);
				Init();
			}
			catch (IOException)
			{
				throw new FatalError("Cannot open file " + fileName);
			}
		}


		public Scanner()
		{

		}

		public static Scanner CreateFromCodeString(string code)
		{
			Scanner scanner = new Scanner();
			scanner.buffer = new StringBuffer(code);
			scanner.NonStreamInit();
			return scanner;
		}

		void NonStreamInit()
		{
			pos = -1; line = 1; col = 0; charPos = -1;
			oldEols = 0;
			NextCh();
			pt = tokens = new Token();  // first token is a dummy
		}

		public Scanner(Stream s)
		{
			buffer = new Buffer(s, true);
			Init();
		}

		void Init()
		{
			pos = -1; line = 1; col = 0; charPos = -1;
			oldEols = 0;
			NextCh();
			if (ch == 0xEF)
			{ // check optional byte order mark for UTF-8
				NextCh(); int ch1 = ch;
				NextCh(); int ch2 = ch;
				if (ch1 != 0xBB || ch2 != 0xBF)
				{
					throw new FatalError(String.Format("illegal byte order mark: EF {0,2:X} {1,2:X}", ch1, ch2));
				}
				buffer = new UTF8Buffer(buffer as Buffer); col = 0; charPos = -1;
				NextCh();
			}
			pt = tokens = new Token();  // first token is a dummy
		}

		private void SetScannerBehindT()
		{
			buffer.Pos = t.pos;
			NextCh();
			line = t.line; col = t.col; charPos = t.charPos;
			for (int i = 0; i < tlen; i++) NextCh();
		}

		// get the next token (possibly a token already seen during peeking)
		public Token Scan()
		{
			if (tokens.next == null)
			{
				return NextToken();
			}
			else
			{
				pt = tokens = tokens.next;
				return tokens;
			}
		}

		// peek for the next token, ignore pragmas
		public Token Peek()
		{
			do
			{
				if (pt.next == null)
				{
					pt.next = NextToken();
				}
				pt = pt.next;
			} while (pt.kind > maxT); // skip pragmas

			return pt;
		}

		// make sure that peeking starts at the current scan position
		public void ResetPeek() { pt = tokens; }

	}

	public partial class Errors
	{
		public class Info
		{
			public string Type;

			public string Text;

			public int Line;

			public int Col;

			public override string ToString()
			{
				return $"({Line},{Col}): {Type} error: {Text}";
			}
		}

		List<Info> ErrInfos = new List<Info>();

		public int Count => ErrInfos.Count;

		public Info this[int index] => ErrInfos[index];

		public void BuildTraceErrorList(Action<string> writelineFunc, string fileName)
		{
			foreach (var err in ErrInfos)
			{
				writelineFunc($"{fileName}{err.ToString()}");
			}
		}

		void AddError(string type, string text, int line, int col)
		{
			var info = new Info
			{
				Text = text,
				Line = line,
				Col = col
			};
			ErrInfos.Add(info);
		}

		public virtual void SemErr(int line, int col, string s)
		{
			AddError("Sem", s, line, col);
		}

		public virtual void SemErr(string s)
		{
			AddError("Sem", s, 0, 0);
		}

		public virtual void Warning(int line, int col, string s)
		{
		}

		public virtual void Warning(string s)
		{
		}

		public virtual void SynErr(int line, int col, int n)
		{
			var s = GetSynErrString(n);
			AddError("Syn", s, line, col);
		}

	}

	internal class Token
	{
		public int kind;    // token kind
		public int pos;     // token position in bytes in the source text (starting at 0)
		public int charPos;  // token position in characters in the source text (starting at 0)
		public int col;     // token column (starting at 1)
		public int line;    // token line (starting at 1)
		public string val;  // token value
		public Token next;  // ML 2005-03-11 Tokens are kept in linked list
	}


	interface IBuffer
	{
		int Read();

		int Peek();

		int Pos { get; set; }
	}


	//-----------------------------------------------------------------------------------
	// Buffer
	//-----------------------------------------------------------------------------------
	internal class Buffer : IBuffer
	{
		// This Buffer supports the following cases:
		// 1) seekable stream (file)
		//    a) whole stream in buffer
		//    b) part of stream in buffer
		// 2) non seekable stream (network, console)

		public const int EOF = char.MaxValue + 1;
		const int MIN_BUFFER_LENGTH = 1024; // 1KB
		const int MAX_BUFFER_LENGTH = MIN_BUFFER_LENGTH * 64; // 64KB
		byte[] buf;         // input buffer
		int bufStart;       // position of first byte in buffer relative to input stream
		int bufLen;         // length of buffer
		int fileLen;        // length of input stream (may change if the stream is no file)
		int bufPos;         // current position in buffer
		Stream stream;      // input stream (seekable)
		bool isUserStream;  // was the stream opened by the user?

		public Buffer(Stream s, bool isUserStream)
		{
			stream = s; this.isUserStream = isUserStream;

			if (stream.CanSeek)
			{
				fileLen = (int)stream.Length;
				bufLen = Math.Min(fileLen, MAX_BUFFER_LENGTH);
				bufStart = Int32.MaxValue; // nothing in the buffer so far
			}
			else
			{
				fileLen = bufLen = bufStart = 0;
			}

			buf = new byte[(bufLen > 0) ? bufLen : MIN_BUFFER_LENGTH];
			if (fileLen > 0) Pos = 0; // setup buffer to position 0 (start)
			else bufPos = 0; // index 0 is already after the file, thus Pos = 0 is invalid
			if (bufLen == fileLen && stream.CanSeek) Close();
		}

		protected Buffer(Buffer b)
		{ // called in UTF8Buffer constructor
			buf = b.buf;
			bufStart = b.bufStart;
			bufLen = b.bufLen;
			fileLen = b.fileLen;
			bufPos = b.bufPos;
			stream = b.stream;
			// keep destructor from closing the stream
			b.stream = null;
			isUserStream = b.isUserStream;
		}

		~Buffer() { Close(); }

		protected void Close()
		{
			if (!isUserStream && stream != null)
			{
				stream.Close();
				stream = null;
			}
		}

		public virtual int Read()
		{
			if (bufPos < bufLen)
			{
				return buf[bufPos++];
			}
			else if (Pos < fileLen)
			{
				Pos = Pos; // shift buffer start to Pos
				return buf[bufPos++];
			}
			else if (stream != null && !stream.CanSeek && ReadNextStreamChunk() > 0)
			{
				return buf[bufPos++];
			}
			else
			{
				return EOF;
			}
		}

		public int Peek()
		{
			int curPos = Pos;
			int ch = Read();
			Pos = curPos;
			return ch;
		}

		// beg .. begin, zero-based, inclusive, in byte
		// end .. end, zero-based, exclusive, in byte
		public string GetString(int beg, int end)
		{
			int len = 0;
			char[] buf = new char[end - beg];
			int oldPos = Pos;
			Pos = beg;
			while (Pos < end) buf[len++] = (char)Read();
			Pos = oldPos;
			return new String(buf, 0, len);
		}

		public int Pos
		{
			get { return bufPos + bufStart; }
			set
			{
				if (value >= fileLen && stream != null && !stream.CanSeek)
				{
					// Wanted position is after buffer and the stream
					// is not seek-able e.g. network or console,
					// thus we have to read the stream manually till
					// the wanted position is in sight.
					while (value >= fileLen && ReadNextStreamChunk() > 0) ;
				}

				if (value < 0 || value > fileLen)
				{
					throw new FatalError("buffer out of bounds access, position: " + value);
				}

				if (value >= bufStart && value < bufStart + bufLen)
				{ // already in buffer
					bufPos = value - bufStart;
				}
				else if (stream != null)
				{ // must be swapped in
					stream.Seek(value, SeekOrigin.Begin);
					bufLen = stream.Read(buf, 0, buf.Length);
					bufStart = value; bufPos = 0;
				}
				else
				{
					// set the position to the end of the file, Pos will return fileLen.
					bufPos = fileLen - bufStart;
				}
			}
		}

		// Read the next chunk of bytes from the stream, increases the buffer
		// if needed and updates the fields fileLen and bufLen.
		// Returns the number of bytes read.
		private int ReadNextStreamChunk()
		{
			int free = buf.Length - bufLen;
			if (free == 0)
			{
				// in the case of a growing input stream
				// we can neither seek in the stream, nor can we
				// foresee the maximum length, thus we must adapt
				// the buffer size on demand.
				byte[] newBuf = new byte[bufLen * 2];
				Array.Copy(buf, newBuf, bufLen);
				buf = newBuf;
				free = bufLen;
			}
			int read = stream.Read(buf, bufLen, free);
			if (read > 0)
			{
				fileLen = bufLen = (bufLen + read);
				return read;
			}
			// end of stream reached
			return 0;
		}
	}

	internal class StringBuffer : IBuffer
	{
		string s;

		int pos;

		public int Pos
		{
			get { return pos; }
			set { pos = value; }
		}

		public StringBuffer(string s)
		{
			this.s = s;
		}

		public int Read()
		{
			if (pos >= s.Length)
				return Buffer.EOF;
			return s[pos++];
		}

		public int Peek()
		{
			return pos >= s.Length ? Buffer.EOF : s[pos];
		}

	}

	//-----------------------------------------------------------------------------------
	// UTF8Buffer
	//-----------------------------------------------------------------------------------
	internal class UTF8Buffer : Buffer
	{
		public UTF8Buffer(Buffer b) : base(b) { }

		public override int Read()
		{
			int ch;
			do
			{
				ch = base.Read();
				// until we find a utf8 start (0xxxxxxx or 11xxxxxx)
			} while ((ch >= 128) && ((ch & 0xC0) != 0xC0) && (ch != EOF));
			if (ch < 128 || ch == EOF)
			{
				// nothing to do, first 127 chars are the same in ascii and utf8
				// 0xxxxxxx or end of file character
			}
			else if ((ch & 0xF0) == 0xF0)
			{
				// 11110xxx 10xxxxxx 10xxxxxx 10xxxxxx
				int c1 = ch & 0x07; ch = base.Read();
				int c2 = ch & 0x3F; ch = base.Read();
				int c3 = ch & 0x3F; ch = base.Read();
				int c4 = ch & 0x3F;
				ch = (((((c1 << 6) | c2) << 6) | c3) << 6) | c4;
			}
			else if ((ch & 0xE0) == 0xE0)
			{
				// 1110xxxx 10xxxxxx 10xxxxxx
				int c1 = ch & 0x0F; ch = base.Read();
				int c2 = ch & 0x3F; ch = base.Read();
				int c3 = ch & 0x3F;
				ch = (((c1 << 6) | c2) << 6) | c3;
			}
			else if ((ch & 0xC0) == 0xC0)
			{
				// 110xxxxx 10xxxxxx
				int c1 = ch & 0x1F; ch = base.Read();
				int c2 = ch & 0x3F;
				ch = (c1 << 6) | c2;
			}
			return ch;
		}
	}

	internal partial class Parser
	{
		const bool _T = true;
		const bool _x = false;
		const int minErrDist = 2;

		public Scanner scanner;
		public Errors errors;

		public Token t;    // last recognized token
		public Token la;   // lookahead token
		int errDist = minErrDist;

		public Parser(Scanner scanner)
		{
			this.scanner = scanner;
			errors = new Errors();
		}

		void SynErr(int n)
		{
			if (errDist >= minErrDist) errors.SynErr(la.line, la.col, n);
			errDist = 0;
		}

		public void SemErr(string msg)
		{
			if (errDist >= minErrDist) errors.SemErr(t.line, t.col, msg);
			errDist = 0;
		}

		public void SemErr(string msg, int line, int col = 1)
		{
			errors.SemErr(line, col, msg);
		}

		void Expect(int n)
		{
			if (la.kind == n) Get(); else { SynErr(n); }
		}

		bool StartOf(int s)
		{
			return set[s, la.kind];
		}

		void ExpectWeak(int n, int follow)
		{
			if (la.kind == n) Get();
			else
			{
				SynErr(n);
				while (!StartOf(follow)) Get();
			}
		}

		bool WeakSeparator(int n, int syFol, int repFol)
		{
			int kind = la.kind;
			if (kind == n) { Get(); return true; }
			else if (StartOf(repFol)) { return false; }
			else
			{
				SynErr(n);
				while (!(set[syFol, kind] || set[repFol, kind] || set[0, kind]))
				{
					Get();
					kind = la.kind;
				}
				return StartOf(syFol);
			}
		}

		bool LaIsSign => la.val == "-" || la.val == "+";


		public static ConstantNode ParseDatum(string input)
		{
			if (string.IsNullOrEmpty(input))
				return null;
			var scanner = Scanner.CreateFromCodeString(input);
			var parser = new Parser(scanner);
			return parser.ParseDatum();
		}

		private ConstantNode ParseDatum()
		{
			la = new Token()
			{
				val = ""
			};
			Get();
			ConstantNode node = null;
			Datum(ref node);
			Expect(0);
			if (errors.Count > 0)
				return null;
			return node;
		}


	}
}
