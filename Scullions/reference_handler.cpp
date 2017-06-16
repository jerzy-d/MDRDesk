#include "stdafx.h"
#include <cstring>
#include "reference_handler.h"


reference_handler::reference_handler(unsigned long long *addresses, int count)
{
	_addresses = addresses;
	_addr_cnt = count;
	_bkwd_ref_counts = new long long[_addr_cnt];
	memset(_bkwd_ref_counts, 0, _addr_cnt * sizeof(long long));
}


reference_handler::~reference_handler()
{
	delete _addresses;
	delete _bkwd_ref_counts;
}

int address_search(unsigned long long* ary, int left, int right, unsigned long long key) {
	key = (key & reference_handler::AddressFlagMask);
	while (left <= right) {
		int middle = (left + right) / 2;
		unsigned long long ary_item = (ary[middle]& reference_handler::AddressFlagMask);
		if (key == ary_item) return middle;
		key < ary_item ? right = middle - 1 : left = middle + 1;
	}
	return -1;
}

void copy_addr_flags(unsigned long long* ary, int from, int to)
{
	unsigned long long fromVal = ary[from];
	unsigned long long toVal = ary[to];
	ary[to] = toVal | (fromVal & reference_handler::AddressMask);
}

bool same_addr_flags(unsigned long long* ary, int lhs, int rhs)
{
	return (ary[lhs] & reference_handler::AddressMask) == (ary[rhs] & reference_handler::AddressMask);
}

void reference_handler::process_parent_refs(unsigned long long parent, int parent_ndx, vector<unsigned long long>& children, vector<int>& data_out) {
	
	data_out.clear();
	data_out.reserve(children.size());
	unsigned long long* begin = &(*children.begin());
	unsigned long long* current = begin;
	unsigned long long* end = &(*children.end());
	unsigned long long* addr_start = _addresses;
	int right = _addr_cnt -1;
	int ndx;
	for (; current < end; ++current) {
		ndx = address_search(begin, 0, right, *current);
		if (ndx < 0) {
			continue;
		}
		data_out.push_back(ndx);

		// update child root flags
		if (!same_addr_flags(begin, parent_ndx, ndx)) {
			if (ndx < parent_ndx) { // in this case we might need to update address flag of current's children
				_reflag_set.insert(ndx);
			}
			copy_addr_flags(begin, parent_ndx, ndx);
		}
	}
}