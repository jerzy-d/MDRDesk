#pragma once

#include <vector>
#include <unordered_set>
#include <algorithm>

//using namespace std;

//struct same_address : std::binary_function<unsigned long long, unsigned long long, bool>
//{
//
//	same_address(unsigned long long val) : _exclude(val) {}
//	bool operator()(unsigned long long a, unsigned long long b) const { return a == b || a == _exclude; }
//
//private:
//	unsigned long long _exclude;
//};


class reference_handler
{
public:
	reference_handler(unsigned long long *addresses, int count, const wchar_t* out_folder);
	~reference_handler();

	unsigned long long* preprocess_parent_refs(std::vector<unsigned long long>& children);
	unsigned long long* preprocess_parent_refs(unsigned long long* begin, unsigned long long* end);
	void process_parent_refs(unsigned long long parent, int parent_ndx, unsigned long long* begin, unsigned long long* end, std::vector<int>& data_out);
	int get_reflag_count();
	int get_notfound_count();
	std::pair<int,int> get_reversed_minmax();

public:


private:
	unsigned long long *_addresses; // heap instance addresses
	int *_reversed_counts; // counts of parents for each child
	int _addr_cnt; // _addresses size
	//long long *_bkwd_ref_counts; // count of parents for each instance
	std::unordered_set<int> _reflag_set; // list of items to reflag
	std::wstring _outFolder;

	int _not_found_count; // count of items not found in instance array

};

