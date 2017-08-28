using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class Constants
	{
		public static int PointerSize = Environment.Is64BitProcess ? 8 : 4;

		public const string CrashDumpFileExt = "dmp";
		public const string TextFileExt = "txt";
        public const string NoValue = "{`??`}";
        public const string NotAvailableValue = "{n/a}";
        public const string FieldTypeNull = "!field-type-null";
		public const string FieldNameNull = "!field-name-null";
		public const string ZeroAddressStr = "0x0000000000000000";
		public const string ZeroStr = "0";
		public const string AdHocQueryDirName = "ad-hoc.queries";
		public const string EmptyStringValue = "\"\"";


		// some standard type names and their ids
		//
		public const string UnknownName = "{unknown}";
		public const string UnknownValue = "{unknown value}";
		public const string NullName = "{null}";
		public const string NullValue = "null";
		public const int NullNameId = 0;

		public const string NullTypeName = "NullType";
        public const string UnknownTypeName = "UnknownType";
        public const string UnknownFieldTypeName = "\u2714UnknownFieldType";
        public const string TypeNotFound = "TypeNotFound";
        public const string FreeTypeName = "Free";
		public const string ErrorTypeName = "ERROR";

		public const string ErrorStr = "ERROR";
		public const int ErrorStrId = 1;
		//public const string Free = "Free";
		public const string SystemObject = "System.Object";
		public const string System__Canon = "System.__Canon";
		public const string SpcPipeNlSep = " ||\r\n";

		// sorts names
		public const string ByCount = "ByCount";
		public const string BySize = "BySize";
		public const string ByTotalSize = "ByTotalSize";
		public const string ByMaxSize = "ByMaxSize";
		public const string ByAvgSize = "ByAvgSize";
		public const string ByTypeName = "ByTypeName";

		public const string ImprobableString = @"\u2700\u2700\u2701u\u2758\u2758\u2759\u27BE\u27BF\u27BF";
        public const char HeavyMultiplication = '\u2716'; // ✖ HEAVY MULTIPLICATION X
        public const string HeavyMultiplicationHeader = "\u2716 "; // ✖ HEAVY MULTIPLICATION X
        public const char HeavyGreekCross = '\u271A'; // ✚ HEAVY GREEK CROSS		
		public const string HeavyGreekCrossPadded = " \u271A "; // ✚ HEAVY GREEK CROSS		
		public const string ReportSeparator = " \u271A "; // ✚ HEAVY GREEK CROSS		
		public const char HeavyAsterisk = '\u2731'; // ✱ heavy asterisk		
        public const string HeavyAsteriskPadded = " \u2731 "; // ✱  heavy asterisk		
        public const string HeavyAsteriskHeader = "\u2731 "; // ✱  heavy asterisk		
        public const string NamespaceSepPadded = " \u2731 "; // ✱  heavy asterisk		
		public const char FancyKleeneStar = '\u2734'; // 2734 ✴ EIGHT POINTED BLACK STAR
		public const char NonValueChar = '\u2734'; // 2734 ✴ EIGHT POINTED BLACK STAR
		public const string NonValue = "\u2734"; // 2734 ✴ EIGHT POINTED BLACK STAR
												 //public const string FieldSymbolPadded = " \u1D4D5 "; // 𝓕 MATHEMATICAL BOLD SCRIPT CAPITAL F
		public const string FieldSymbolPadded = " \u2131 "; // ℱ SCRIPT CAPITAL F
															//public const string InformationSymbolPadded = " \u2111 "; // ℑ BLACK-LETTER CAPITAL I
															// 2139 ℹ INFORMATION SOURCE
		public const string InformationSymbolHeader = "\u2110 "; // ℐ SCRIPT CAPITAL I
		public const char InformationSymbol = '\u2110'; // ℐ SCRIPT CAPITAL I
		public const char HeavyRightArrow = '\u279C'; // ➜ HEAVY ROUND-TIPPED RIGHTWARDS ARROW
		public const string HeavyRightArrowPadded = " \u279C "; // ➜ HEAVY ROUND-TIPPED RIGHTWARDS ARROW
		public const string HeavyRightArrowHeader = "\u279C "; // ➜ HEAVY ROUND-TIPPED RIGHTWARDS ARROW
		public const string FailureSymbolHeader = "\u2132 "; // Ⅎ TURNED CAPITAL F
		public const string WindowsNewLine = " \u27A5 "; // ➥ HEAVY BLACK CURVED DOWNWARDS AND RIGHTWARDS ARROW
		public const string UnixNewLine = " \u27A6 "; // ➦ HEAVY BLACK CURVED UPWARDS AND RIGHTWARDS ARROW
		public const char WindowsNewLineChar = '\u27A5'; // ➥ HEAVY BLACK CURVED DOWNWARDS AND RIGHTWARDS ARROW
		public const char UnixNewLineChar = '\u27A6'; // ➦ HEAVY BLACK CURVED UPWARDS AND RIGHTWARDS ARROW

		public const char HeavyCheckMark = '\u2714'; // ✔ HEAVY CHECK MARK
		public const string HeavyCheckMarkPadded = " \u2714 ";  // ✔ HEAVY CHECK MARK
		public const string HeavyCheckMarkHeader = "\u2714 ";  // ✔ HEAVY CHECK MARK
		public const char BlackDiamond = '\u2756'; // ❖ BLACK DIAMOND MINUS WHITE X 
		public const string BlackDiamondHeader = "\u2756 "; // ❖ BLACK DIAMOND MINUS WHITE X 
		public const string BlackDiamondPadded = " \u2756 "; // ❖ BLACK DIAMOND MINUS WHITE X 
		public const char HeavyVerticalBar = '\u275A'; // ❚ HEAVY VERTICAL BAR
		public const char MediumVerticalBar = '\u2759'; // ❙ MEDIUM VERTICAL BAR
		public const string MediumVerticalBarPadded = " \u2759 "; // ❙ MEDIUM VERTICAL BAR
		public const string BlackFourPointedStarHeader = " \u2726 "; // ✦ BLACK FOUR POINTED STAR 

		public const char BlackTriangle = '\u25BC'; // ▼  black down-pointing triangle
		public const char ShadowedWhiteSquare = '\u274F'; // ❏ LOWER RIGHT DROP-SHADOWED WHITE SQUARE
		public const char AdhocQuerySymbol = '\u274F'; // ❏ LOWER RIGHT DROP-SHADOWED WHITE SQUARE

		public const string NewLineDisp = "\u2B92 "; // NEWLINE LEFT
		public const string ReturnDisp = "\u27a7 "; // ➧ SQUAT BLACK RIGHTWARDS ARROW
		public const char NewLineDispChar = '\u2B92'; // NEWLINE LEFT
		public const char RightSquatArrow = '\u27a7'; // ➧ SQUAT BLACK RIGHTWARDS ARROW
                                                      //public const char RightArrow = '\u2B95'; // RIGHTWARDS BLACK ARROW
        public const char DownwardsBlackArrow = '\u2B07'; // ⬇ DOWNWARDS BLACK ARROW
        public const string RightTrianglePadded = " \u23F5 "; // BLACK MEDIUM RIGHT-POINTING TRIANGLE
		public const char LeftCurlyBracket = '\u2774'; // ❴ MEDIUM LEFT CURLY BRACKET ORNAMENT
		public const char RightCurlyBracket = '\u2775'; // ❵ MEDIUM RIGHT CURLY BRACKET ORNAMENT
														// 24EA ⓪ CIRCLED DIGIT ZERO
														// 2070 ⁰ SUPERSCRIPT ZERO
														// 2080 ₀ SUBSCRIPT ZERO
														// 2466  ⑦  Circled Digit Seven 
														// 2474  ⑴  Parenthesized Digit One
		public const string HeavyLeftAngleBracketPadded = " \u2770 "; // ❰ HEAVY LEFT-POINTING ANGLE BRACKET
		public const string HeavyRightAngleBracketPadded = " \u2771 "; // ❱ HEAVY RIGHT-POINTING ANGLE BRACKET
		public const string HeavyBallotX = " \u2718 "; // ✘ HEAVY BALLOT X

		public const char LeftwardsDoubleArrow = '\u2906'; // ⤆ LEFTWARDS DOUBLE ARROW FROM BAR
		public const char RightwardsDoubleArrow = '\u2907'; // ⤇ RIGHTWARDS DOUBLE ARROW FROM BAR


		public const char StructChar = '\u211C'; // ℜ BLACK-LETTER CAPITAL R
		public const string StructHeader = " \u211C\u279C "; // ℜ BLACK-LETTER CAPITAL R
		public const char InterfaceChar = '\u2111'; // ℑ BLACK-LETTER CAPITAL I
		public const string InterfaceHeader = " \u2111\u279C "; // ℑ BLACK-LETTER CAPITAL I
		public const char ClassChar = '\u212D'; // ℭ BLACK-LETTER CAPITAL C
		public const string ClassHeader = " \u212D\u279C "; // ℭ BLACK-LETTER CAPITAL C
		public const char PrimitiveChar = '\u2119'; // ℙ DOUBLE-STRUCK CAPITAL P
		public const string PrimitiveHeader = " \u2119\u279C "; // ℙ DOUBLE-STRUCK CAPITAL P
		public const char ArrayChar = '\u213F'; // ℿ DOUBLE-STRUCK CAPITAL PI
		public const string ArrayHeader = " \u213F\u279C "; // ℿ DOUBLE-STRUCK CAPITAL PI
															//public const char FilterChar = '\u2132'; // Ⅎ TURNED CAPITAL F
															//public const string FilterHeader = " \u2132\u279C "; // Ⅎ TURNED CAPITAL F
		//public const char FilterChar = '\u2718'; // ✘ HEAVY BALLOT X
		public const char FilterChar = '\u23EC'; // ⏬ Black down-pointing double triangle
												 //public const string FilterHeader = " \u2718 "; // ✘ HEAVY BALLOT X
		public const string FilterHeader = " \u23EC "; // ⏬ Black down-pointing double triangle

		public const char RightSolidArrow = '\u279E'; // ➞ HEAVY TRIANGLE-HEADED RIGHTWARDS ARROW
		public const string RightSolidArrowPadded = " \u279E "; // ➞ HEAVY TRIANGLE-HEADED RIGHTWARDS ARROW
		public const char RightDashedArrow = '\u279F'; // ➟ DASHED TRIANGLE-HEADED RIGHTWARDS ARROW
		public const string RightDashedArrowPadded = " \u279F "; // ➟ DASHED TRIANGLE-HEADED RIGHTWARDS ARROW

        public const char HorizontalEllipsisChar = '\u2026'; // … HORIZONTAL ELLIPSIS
        public const char CircledOneChar = '\u2776'; // ❶ DINGBAT NEGATIVE CIRCLED DIGIT ONE
        public const char CircledTwoChar = '\u2777'; // ❷ DINGBAT NEGATIVE CIRCLED DIGIT TWO
        public const char CircledThreeChar = '\u2778'; // ❸ DINGBAT NEGATIVE CIRCLED DIGIT THREE

        public const char CircledFourChar = '\u2779'; // ❹ DINGBAT NEGATIVE CIRCLED DIGIT FOUR
        public const char CircledFiveChar = '\u277A'; // ❺ DINGBAT NEGATIVE CIRCLED DIGIT FIVE
        public const char CircledSixChar = '\u277B'; // ❻ DINGBAT NEGATIVE CIRCLED DIGIT SIX
        public const char CircledSevenChar = '\u277C'; // ❼ DINGBAT NEGATIVE CIRCLED DIGIT SEVEN
        public const char CircledEightChar = '\u277D'; // ❽ DINGBAT NEGATIVE CIRCLED DIGIT EIGHT
        public const char CircledNineChar = '\u277E'; // ❾ DINGBAT NEGATIVE CIRCLED DIGIT NINE
        public const char CircledTenChar = '\u277F'; // ❿ DINGBAT NEGATIVE CIRCLED NUMBER TEN

        public static string LockStr = char.ConvertFromUtf32(0x1F512);
        public static string UnlockStr = char.ConvertFromUtf32(0x1F513);

        //27A0 ➠ HEAVY DASHED TRIANGLE-HEADED RIGHTWARDS ARROW 
        //27A1 ➡ BLACK RIGHTWARDS ARROW
        //279D ➝ TRIANGLE-HEADED RIGHTWARDS ARROW

        public char[] SubDigits = new[]
		{
			'\u2080', // ₀ SUBSCRIPT ZERO
		};


		public const string Unknown = "UNKNOWN";

		public const int ClrElementTypeMaxValue = (int)ClrElementType.SZArray;

		public const int InvalidIndex = -1;
		public const uint InvalidHalfIndexMSB = 0xFFFF0000;

		public const uint InvalidThreadId = uint.MaxValue;

		public const ulong InvalidAddress = 0ul;
        public const int AddressStringLength = 16;

		public const string ReportPath = @"ad-hoc.queries";


		public const string MapRefFwdDataFilePostfix = ".`REFFWDDATA[0].bin";
		public const string MapRefFwdOffsetsFilePostfix = ".`FWDREFOFFSETS[0].bin";
		public const string MapFwdRefsFilePostfix = ".`FWDREFS[0].bin";
		public const string MapRefBwdOffsetsFilePostfix = ".`BWDREFOFFSETS[0].bin";
		public const string MapBwdRefsFilePostfix = ".`BWDREFS[0].bin";

		// TODO JRD -- new below, delete old field refs files above

		// new stuff ////////////////////////////////////////////////////////////////////////////////////////////////////////////

		// types
		//
		public const string TxtTypeNamesFilePostfix = ".`TYPENAMES[0].txt"; // list of all type names, not all of them are in the heap
		public const string TxtReversedTypeNamesFilePostfix = ".`REVERSEDTYPENAMES[0].txt"; // type name followed by namespace, for one of type grids
        public const string MapKindsFilePostfix = ".`KINDS[0].bin"; // our special type kind to ease digging up values 
        public const string MapTypeFieldTypesPostfix = ".`TYPEFIELDTYPES0].bin"; // type ids of field types for each type 
        public const string MapFieldTypeParentTypesPostfix = ".`FIELDTYPEPARENTTYPES0].bin"; // reversed references of the above (MapTypeFieldTypesPostfix)

        // segmnents, generation info
        //
        public const string MapSegmentInfoFilePostfix = ".`SEGMENTS[0].bin";

		// roots
		//
		public const string MapRootsInfoFilePostfix = ".`ROOTS[0].bin"; // ClrtRoot structures
		public const string MapRootsAddressesFilePostfix = ".`ROOTADDRESSES[0].bin"; // root addresses and their map to ClrtRoot array
		public const string MapRootsObjectsFilePostfix = ".`ROOTOBJECTS[0].bin"; // root objects and their map to ClrtRoot array
		public const string MapRootsFinalizerFilePostfix = ".`FINALIZER[0].bin"; // list of instance addresses


		// instance addresses and their types
		//
		public const string MapInstancesFilePostfix = ".`INSTANCES[0].bin"; // instance addresses and corresponding type ids
		public const string MapInstanceTypesFilePostfix = ".`INSTANCETYPES[0].bin"; // instance addresses and corresponding type ids
		public const string MapInstanceSizesFilePostfix = ".`INSTANCESIZES[0].bin"; // instance addresses and corresponding type ids
		public const string MapInstanceBaseSizesFilePostfix = ".`INSTANCEBASESIZES[0].bin"; // instance addresses and corresponding type ids
		public const string MapArraySizesFilePostfix = ".`ARRAYSIZES[0].bin"; //
		public const string MapTypeInstanceOffsetsFilePostfix = ".`TYPEINSTANCEOFFSETS[0].bin"; // instance addresses and corresponding type ids
		public const string MapTypeInstanceMapFilePostfix = ".`TYPEINSTANCEMAP[0].bin"; // instance addresses and corresponding type ids


		// instance references
		//
		public const string MapRefsObjectFieldPostfix = ".`REFSOBJECTFIELD[0].bin"; // 
		public const string MapRefsFieldObjectPostfix = ".`REFSFIELDOBJECT[0].bin";
		public const string TxtCommonStringIdsPostfix = ".`STRINGIDS[0].txt"; // list of strings, ordered by instance ids

		public const string MapThreadsAndBlocksFilePostfix = ".`THREADSANDBLOCKS[0].bin";
		public const string MapThreadsAndBlocksGraphFilePostfix = ".`THREADSANDBLOCKSGRAPH[0].bin";
		public const string TxtThreadFrameDescriptionFilePostfix = ".`THREADFRAMEDESCRIPTION[0].txt";
		public const string MapThreadFramesFilePostfix = ".`THREADFRAMES[0].bin";


		// end of new stuff ///////////////////////////////////////////////////////////////////////////////////////////////////////

		public const string TxtDumpStringsPostfix = ".DUMPSTRINGS[0].txt"; // list of all strings in dump, ordered by StringComparer.Ordinal
		public const string MapDumpStringsInfoPostfix = ".DUMPSTRINGSINFO[0].map"; // information about dump strings: addresses, sizes, etc

		// TODO JRD not considered yet 

		public const string TxtTargetModulesPostfix = ".~TARGETMODULES.txt";
		public const string TxtIndexInfoFilePostfix = ".~INDEXINFO.txt";
		public const string TxtIndexingInfoFilePostfix = ".~INDEXINGINFO.txt";
        public const string TxtIndexErrorsFilePostfix = ".~INDEXERRORS[0].txt";

        public const string MapArrayInstanceFilePostfix = ".ARRAYINSTANCES[0].map";

		public const string MapHeapFreeFilePostfix = ".HEAPFREE[0].map";

		public const string MapMultiParentsFilePostfix = ".MULTIPARENTS[0].map";

		public const string MapSegmentFilePostfix = ".SEGMENTS[0].map";

		public const string TxtTypeFilePostfix = ".TYPENAMES[0].txt";
		public const string TxtReversedTypeNameFilePostfix = ".REVERSEDTYPENAMES[0].txt";
		public const string TxtRootFilePostfix = ".ROOTNAMES[0].txt";

		public const string TxtTypeEmbededFilePostfix = ".TYPEEMBEDEDNAMES.txt";
		public const string TxtTypeMtEnumerationPostfix = ".TYPEMTENUMERATION.txt";

		public const string MapTypeFieldIndexFilePostfix = ".TYPEFIELDINDEX[0].map";
		public const string MapFieldTypeMapFilePostfix = ".FIELDTYPEMAP[0].map";

		//// roots files
		////
		//public const string MapFinalizerFilePostfix = ".FINALIZER[0].map"; // list of instance addresses
		//public const string TxtFinalizerFilePostfix = ".FINALIZER[0].txt"; // debugging/testing only
		//public const string MapRootsFilePostfix = ".ROOTS[0].map"; // ClrtRoot structures
		//public const string MapRootAddressesFilePostfix = ".ROOTADDRESSES[0].map"; // root addresses and their map to ClrtRoot array
		//public const string MapRootObjectsFilePostfix = ".ROOTOBJECTS[0].map"; // root objects and their map to ClrtRoot array
		//public const string MapUnrootedAddressesFilePostfix = ".UNROOTEDADDRESSES[0].map"; // addresses with no reference

		//public const string MapTypeBaseAndElementFilePostfix = ".TYPEBASEANDELEMENTFILE[0].map";
		//public const string MapTypeFieldCountsFilePostfix = ".TYPEFIELDCOUNTS[0].map";
		//public const string MapTypeIntanceCountsFilePostfix = ".TYPEINTANCECOUNTS[0].map";


		//public const string MapArraysFilePostfix = ".ARRAYS[0].map";
		//public const string MapArrayContentsFilePostfix = ".ARRAYCONTENTS[0].map";
		//public const string TxtArrayValuesFilePostfix = ".ARRAYVALUES[0].txt";


		//public const string TxtUnprocessedTypesFilePostfix = ".UNPROCESSEDTYPES.txt";


		//public const string MapFinalizerObjectAddressesPostfix = ".FINALIZEROBJECTADDRESSES[0].map";

		//public const string TxtThreadsAndBlocksFilePostfix = ".THREADSANDBLOCKS[0].txt";
		//public const string MapThreadsAndBlocksFilePostfix = ".THREADSANDBLOCKS[0].map";




	}
}
