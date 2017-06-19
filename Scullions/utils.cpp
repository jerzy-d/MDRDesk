#include "stdafx.h"
#include "utils.h"

using namespace std;
static const unsigned long long AddressFlagMask = 0x00FFFFFFFFFFFFFF;
static const unsigned long long AddressMask = 0xFF00000000000000;

void* open_write_file(wstring& path) {
	return CreateFile2(path.c_str(), GENERIC_WRITE, 0, CREATE_ALWAYS, NULL);
}

void* open_read_file(wstring& path) {
	return CreateFile2(path.c_str(), GENERIC_READ, 0, OPEN_EXISTING, NULL);
}

void close_file(void* hnd) {
	CloseHandle(hnd);
}

void  write_file(void* hnd, __int8* bytes, int len) {
	DWORD written;
	WriteFile(hnd, bytes, len, &written, NULL);
}

void  read_file(void* hnd, void* bytes, int len) {
	DWORD written;
	ReadFile(hnd, bytes, len, &written, NULL);
}

void copy_addr_flags(unsigned long long* ary, int from, int to) {
	unsigned long long fromVal = ary[from];
	unsigned long long toVal = ary[to];
	ary[to] = toVal | (fromVal & AddressMask);
}

bool same_addr_flags(unsigned long long* ary, int lhs, int rhs)
{
	return (ary[lhs] & AddressMask) == (ary[rhs] & AddressMask);
}

int address_search(unsigned long long* ary, int left, int right, unsigned long long key) {
	key = (key & AddressFlagMask);
	while (left <= right) {
		int middle = (left + right) / 2;
		unsigned long long ary_item = (ary[middle] & AddressFlagMask);
		if (key == ary_item) return middle;
		key < ary_item ? right = middle - 1 : left = middle + 1;
	}
	return -1;
}

