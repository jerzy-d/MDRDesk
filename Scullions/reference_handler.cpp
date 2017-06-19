#include "stdafx.h"
#include <cstring>
#include "utils.h"
#include "reference_handler.h"

using namespace std;

reference_handler::reference_handler(unsigned long long *addresses, int count, const wchar_t* out_folder) : _addresses(addresses), _addr_cnt(count), _outFolder(out_folder)
{
	_reversed_counts = new int[count];
	memset(_reversed_counts, 0, count * sizeof(int));
}

reference_handler::~reference_handler()
{
	delete _addresses;
	//delete _bkwd_ref_counts;
	delete _reversed_counts;
}

unsigned long long* reference_handler::preprocess_parent_refs(vector<unsigned long long>& children) {
	sort(children.begin(), children.end());
	unsigned long long* begin = &(*children.begin());
	unsigned long long* end = begin + children.size();
	return unique(begin, end);
}

unsigned long long* reference_handler::preprocess_parent_refs(unsigned long long* begin, unsigned long long* end) {
	sort(begin, end);
	return unique(begin, end);
}

int reference_handler::get_reflag_count() {
	return _reflag_set.size();
}

int reference_handler::get_notfound_count() {
	return _reflag_set.size();
}

std::pair<int,int> reference_handler::get_reversed_minmax() {
	int min = 0x7fffffff;
	int max = 0;
	for (int *begin = _reversed_counts, *end = _reversed_counts + _addr_cnt; begin < end; ++begin) {
		int val = *begin;
		if (val > 0 && min > val) min = val;
		if (max < val) max = val;
	}
	return pair<int, int>(min, max);
}

void reference_handler::process_parent_refs(unsigned long long parent, int parent_ndx, unsigned long long* begin, unsigned long long* end, vector<int>& data_out) {

	data_out.clear();
	data_out.reserve(distance(begin, end));
	unsigned long long* current = begin;
	unsigned long long* addr_start = _addresses;
	int right = _addr_cnt - 1;
	int ndx;
	for (; current < end; ++current) {
		unsigned long long addr = *current;
		if (addr == parent) continue;
		ndx = address_search(_addresses, 0, right, addr);
		if (ndx < 0) {
			++_not_found_count;
			continue;
		}
		data_out.push_back(ndx);
		_reversed_counts[ndx] += 1;

		// update child root flags
		if (!same_addr_flags(_addresses, parent_ndx, ndx)) {
			if (ndx < parent_ndx) { // in this case we might need to update address flag of current's children
				_reflag_set.insert(ndx);
			}
			copy_addr_flags(_addresses, parent_ndx, ndx);
		}
	}
}




