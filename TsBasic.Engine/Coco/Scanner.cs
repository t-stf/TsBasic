
using System;
using System.IO;
using System.Collections.Generic;

namespace TsBasic {



//-----------------------------------------------------------------------------------
// Scanner
//-----------------------------------------------------------------------------------
internal partial class Scanner {
	const int maxT = 52;
	const int noSym = 52;
	char valCh;       // current input character (for token.val)

	
	static Scanner() {
		start = new Dictionary<int, int>(128);
		for (int i = 97; i <= 122; ++i) start[i] = 18;
		for (int i = 48; i <= 57; ++i) start[i] = 19;
		for (int i = 34; i <= 34; ++i) start[i] = 13;
		for (int i = 10; i <= 10; ++i) start[i] = 15;
		for (int i = 43; i <= 43; ++i) start[i] = 17;
		for (int i = 45; i <= 45; ++i) start[i] = 17;
		start[46] = 6; 
		start[59] = 20; 
		start[44] = 21; 
		start[40] = 22; 
		start[41] = 23; 
		start[42] = 31; 
		start[47] = 24; 
		start[94] = 25; 
		start[61] = 27; 
		start[60] = 32; 
		start[62] = 33; 
		start[Buffer.EOF] = -1;

	}
	
	void NextCh() {
		if (oldEols > 0) { ch = EOL; oldEols--; } 
		else {
			pos = buffer.Pos;
			// buffer reads unicode chars, if UTF8 has been detected
			ch = buffer.Read(); col++; charPos++;
			// replace isolated '\r' by '\n' in order to make
			// eol handling uniform across Windows, Unix and Mac
			if (ch == '\r' && buffer.Peek() != '\n') ch = EOL;
			if (ch == EOL) { line++; col = 0; }
		}
		if (ch != Buffer.EOF) {
			valCh = (char) ch;
			ch = char.ToLower((char) ch);
		}

	}

	void AddCh() {
		if (tlen >= tval.Length) {
			char[] newBuf = new char[2 * tval.Length];
			Array.Copy(tval, 0, newBuf, 0, tval.Length);
			tval = newBuf;
		}
		if (ch != Buffer.EOF) {
			tval[tlen++] = valCh;
			NextCh();
		}
	}




	void CheckLiteral() {
		switch (t.val.ToLower()) {
			case "end": t.kind = 11; break;
			case "stop": t.kind = 12; break;
			case "print": t.kind = 13; break;
			case "tab": t.kind = 16; break;
			case "let": t.kind = 29; break;
			case "rem": t.kind = 30; break;
			case "goto": t.kind = 31; break;
			case "gosub": t.kind = 32; break;
			case "go": t.kind = 33; break;
			case "to": t.kind = 34; break;
			case "sub": t.kind = 35; break;
			case "return": t.kind = 36; break;
			case "if": t.kind = 37; break;
			case "then": t.kind = 38; break;
			case "for": t.kind = 39; break;
			case "step": t.kind = 40; break;
			case "next": t.kind = 41; break;
			case "data": t.kind = 42; break;
			case "read": t.kind = 43; break;
			case "restore": t.kind = 44; break;
			case "dim": t.kind = 45; break;
			case "option": t.kind = 46; break;
			case "base": t.kind = 47; break;
			case "on": t.kind = 48; break;
			case "randomize": t.kind = 49; break;
			case "def": t.kind = 50; break;
			case "input": t.kind = 51; break;
			default: break;
		}
	}

	Token NextToken() {
		while (ch == ' ' ||
			ch == 9 || ch == 13
		) NextCh();

		int recKind = noSym;
		int recEnd = pos;
		t = new Token();
		t.pos = pos; t.col = col; t.line = line; t.charPos = charPos;
		int state;
		state = start.ContainsKey(ch) ? start[ch] : 0;
		tlen = 0; AddCh();
		
		switch (state) {
			case -1: { t.kind = eofSym; break; } // NextCh already done
			case 0: {
				if (recKind != noSym) {
					tlen = recEnd - t.pos;
					SetScannerBehindT();
				}
				t.kind = recKind; break;
			} // NextCh already done
			case 1:
				{t.kind = 2; break;}
			case 2:
				recEnd = pos; recKind = 4;
				if (ch >= '0' && ch <= '9') {AddCh(); goto case 2;}
				else if (ch == 'e') {AddCh(); goto case 3;}
				else {t.kind = 4; break;}
			case 3:
				if (ch >= '0' && ch <= '9') {AddCh(); goto case 5;}
				else if (ch == '+' || ch == '-') {AddCh(); goto case 4;}
				else {goto case 0;}
			case 4:
				if (ch >= '0' && ch <= '9') {AddCh(); goto case 5;}
				else {goto case 0;}
			case 5:
				recEnd = pos; recKind = 4;
				if (ch >= '0' && ch <= '9') {AddCh(); goto case 5;}
				else {t.kind = 4; break;}
			case 6:
				recEnd = pos; recKind = 5;
				if (ch >= '0' && ch <= '9') {AddCh(); goto case 6;}
				else if (ch == 'e') {AddCh(); goto case 7;}
				else {t.kind = 5; break;}
			case 7:
				if (ch >= '0' && ch <= '9') {AddCh(); goto case 9;}
				else if (ch == '+' || ch == '-') {AddCh(); goto case 8;}
				else {goto case 0;}
			case 8:
				if (ch >= '0' && ch <= '9') {AddCh(); goto case 9;}
				else {goto case 0;}
			case 9:
				recEnd = pos; recKind = 5;
				if (ch >= '0' && ch <= '9') {AddCh(); goto case 9;}
				else {t.kind = 5; break;}
			case 10:
				if (ch >= '0' && ch <= '9') {AddCh(); goto case 12;}
				else if (ch == '+' || ch == '-') {AddCh(); goto case 11;}
				else {goto case 0;}
			case 11:
				if (ch >= '0' && ch <= '9') {AddCh(); goto case 12;}
				else {goto case 0;}
			case 12:
				recEnd = pos; recKind = 6;
				if (ch >= '0' && ch <= '9') {AddCh(); goto case 12;}
				else {t.kind = 6; break;}
			case 13:
				if (ch == '"') {AddCh(); goto case 14;}
				else if (ch <= '!' || ch >= '#' && ch <= 65535) {AddCh(); goto case 13;}
				else {goto case 0;}
			case 14:
				{t.kind = 7; break;}
			case 15:
				{t.kind = 8; break;}
			case 16:
				{t.kind = 9; break;}
			case 17:
				{t.kind = 10; break;}
			case 18:
				recEnd = pos; recKind = 1;
				if (ch >= '0' && ch <= '9' || ch >= 'a' && ch <= 'z') {AddCh(); goto case 18;}
				else if (ch == ':') {AddCh(); goto case 1;}
				else if (ch == '$') {AddCh(); goto case 16;}
				else {t.kind = 1; t.val = new String(tval, 0, tlen); CheckLiteral(); return t;}
			case 19:
				recEnd = pos; recKind = 3;
				if (ch >= '0' && ch <= '9') {AddCh(); goto case 19;}
				else if (ch == '.') {AddCh(); goto case 2;}
				else if (ch == 'e') {AddCh(); goto case 10;}
				else {t.kind = 3; break;}
			case 20:
				{t.kind = 14; break;}
			case 21:
				{t.kind = 15; break;}
			case 22:
				{t.kind = 17; break;}
			case 23:
				{t.kind = 18; break;}
			case 24:
				{t.kind = 20; break;}
			case 25:
				{t.kind = 21; break;}
			case 26:
				{t.kind = 22; break;}
			case 27:
				{t.kind = 23; break;}
			case 28:
				{t.kind = 24; break;}
			case 29:
				{t.kind = 25; break;}
			case 30:
				{t.kind = 26; break;}
			case 31:
				recEnd = pos; recKind = 19;
				if (ch == '*') {AddCh(); goto case 26;}
				else {t.kind = 19; break;}
			case 32:
				recEnd = pos; recKind = 27;
				if (ch == '>') {AddCh(); goto case 28;}
				else if (ch == '=') {AddCh(); goto case 29;}
				else {t.kind = 27; break;}
			case 33:
				recEnd = pos; recKind = 28;
				if (ch == '=') {AddCh(); goto case 30;}
				else {t.kind = 28; break;}

		}
		t.val = new String(tval, 0, tlen);
		return t;
	}
	


} // end Scanner

}