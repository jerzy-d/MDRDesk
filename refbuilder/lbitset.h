#pragma once
#include <assert.h>
#include <cstdint>
#include <vector>


class lbitset
{
	const int BitCount = sizeof(uint64_t) * 8; // ??

public:
	uint64_t * _bits;
	int _size;
	int _bits_len;
	int _set_count;

	lbitset() : _bits{ nullptr }, _size{ 0 }, _bits_len{ 0 }, _set_count{ 0 } {
	}

	~lbitset() {
		delete _bits;
	}

	lbitset(int size) : _size{ size }, _set_count{ 0 }
	{
		int cnt = size / BitCount + ((size%BitCount) > 0 ? 1 : 0);
		_bits = new uint64_t[cnt];
		for (int i = 0; i < cnt; ++i) {
			_bits[i] = (uint64_t)0;
		}
		_size = size;
		_set_count = 0;
		_bits_len = cnt;
	}

	void init(int size)
	{
		delete _bits;
		int cnt = size / BitCount + ((size%BitCount) > 0 ? 1 : 0);
		_bits = new uint64_t[cnt];
		for (int i = 0; i < cnt; ++i) {
			_bits[i] = (uint64_t)0;
		}
		_size = size;
		_set_count = 0;
		_bits_len = cnt;
	}


	int set_count() {
		return _set_count;
	}

	int get_next_set(int i) {
		++i;
		int ndx = i / BitCount;
		if (_bits[ndx] != 0)
		{
			uint64_t bits = _bits[ndx];
			uint64_t bit = (uint64_t)1 << (i%BitCount);
			while (bit > 0) {
				if ((bits&bit) != 0) {
					return i;
				}
				bit <<= 1;
				++i;
			}
		}
		++ndx;
		while (ndx < _bits_len && _bits[ndx] == 0) {
			++ndx;
		}
		int base = ndx * BitCount;
		if (ndx < _bits_len) {
			uint64_t bits = _bits[ndx];
			assert(bits != 0);
			uint64_t bit = 1UL;
			while (bit > 0) {
				if ((bits&bit) != 0) {
					return base;
				}
				bit <<= 1;
				++base;
			}
		}
		return -1;
	}

	std::vector<int> unset_indices() {
		if (_set_count < 1) return std::vector<int>();
		std::vector<int> ary(_size - _set_count);
		int ndx = 0;
		for (int i = 0, icnt = _size; i < icnt; ++i) {
			if (is_set(i)) continue;
			ary.push_back(i);
		}
		return ary;
	}

	void set(int i) {
		if (!is_set(i)) {
			++_set_count;
			_bits[i / BitCount] |= ((uint64_t)1 << (i%BitCount));
		}
	}

	void reset(int i) {
		if (is_set(i)) {
			--_set_count;
			_bits[i / BitCount] &= ~((uint64_t)1 << (i % BitCount));
		}
	}

	bool is_set(int i)
	{
		return (_bits[i / BitCount] & ((uint64_t)1 << (i % BitCount))) != 0;
	}

};

