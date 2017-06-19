#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h> 
#include <string>
#include <vector>
#include <unordered_set>
#include <algorithm>

void* open_write_file(std::wstring& path);
void copy_addr_flags(unsigned long long* ary, int from, int to);
bool same_addr_flags(unsigned long long* ary, int lhs, int rhs);
int address_search(unsigned long long* ary, int left, int right, unsigned long long key);
