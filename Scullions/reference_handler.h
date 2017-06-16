#pragma once

#include <vector>
#include <unordered_set>
#include <algorithm>

using namespace std;

class reference_handler
{
public:
	reference_handler(unsigned long long *addresses, int count);
	~reference_handler();

	void process_parent_refs(unsigned long long parent, int parent_ndx, vector<unsigned long long>& children, vector<int>& data_out);
	//int address_search(unsigned long long* ary, int left, int right, unsigned long long key);

public:
	static const unsigned long long AddressFlagMask =	0x00FFFFFFFFFFFFFF;
	static const unsigned long long AddressMask =		0xFF00000000000000;

private:
	unsigned long long *_addresses; // heap instance addresses
	int _addr_cnt; // _addresses size
	long long *_bkwd_ref_counts; // count of parents for each instance
	std::unordered_set<int> _reflag_set;

};

