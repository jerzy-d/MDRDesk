#pragma once

#include <Windows.h>
#include <iostream>
#include <memory>
#include <cstdint>
#include <array>
#include <string>
#include <sstream>
#include <vector>
#include <algorithm>
#include <assert.h>
#include "lbitset.h"

static const uint64_t FlagMask = 0xFF00000000000000;
static const uint64_t AddrMask = 0x00FFFFFFFFFFFFFF;
static const uint64_t Rooted = 0x8000000000000000;
static const uint64_t Finalizer = 0x4000000000000000;
static const uint64_t Root = 0x2000000000000000;
static const uint64_t Local = 0x1000000000000000;
static const uint64_t NotRoot = ~0x2000000000000000;
static const uint64_t RootedFinalizer = (Rooted | Finalizer);

struct addr_less {
	bool operator()(uint64_t a, uint64_t b) const
	{
		return (AddrMask & a) < (AddrMask & b);
	}
};

struct addr_eq {
	bool operator()(uint64_t a, uint64_t b) const
	{
		return (AddrMask & a) == (AddrMask & b);
	}
};

class ref_builder
{
public:

	static std::pair<int, uint64_t*> read_addresses(const wchar_t* path) {

		HANDLE hFile;
		DWORD  dwBytesRead = 0;
		const DWORD readRequest = 32 * 1024;
		char   ReadBuffer[readRequest];

		hFile = CreateFile(path, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
		if (!is_valid_handle(hFile)) { return std::make_pair(-1, nullptr); }

		// read count of items
		ReadFile(hFile, ReadBuffer, 4, &dwBytesRead, nullptr);

		uint32_t count = *((uint32_t*)ReadBuffer);
		uint64_t* ary = new uint64_t[count];
		uint32_t ndx{ 0 };
		dwBytesRead = 0;
		DWORD off{ 0 };

		while (ndx < count) {
			ReadFile(hFile, ReadBuffer + off, readRequest - off, &dwBytesRead, nullptr);
			char* begin = ReadBuffer;
			char* end = ReadBuffer + (dwBytesRead + off);
			while (begin < end && end - begin >= sizeof(uint64_t)) {
				ary[ndx++] = *((uint64_t*)begin);
				begin += sizeof(uint64_t);
			}
			if (end > begin) {
				off = end - begin;
				memmove(ReadBuffer, begin, off);
			}
			else {
				off = 0;
			}
		}
		CloseHandle(hFile);
		return std::make_pair(count, ary);
	}

	static bool read_fwdrefs(const wchar_t* path_addr_refs, const wchar_t* path_fwd_offs, const wchar_t* path_fwd_refs,
		uint64_t* addresses, int addr_cnt, lbitset& bit_set, int* bwdcounts) {

		HANDLE hFile = INVALID_HANDLE_VALUE, hFileOffs = INVALID_HANDLE_VALUE, hFileRefs = INVALID_HANDLE_VALUE;
		DWORD  bytes_read = 0;
		const DWORD read_req = sizeof(uint64_t) * 1024 * 32;
		char   read_buff[read_req];
		bool read_cnt = true;
		uint32_t count = 0;
		std::vector<uint64_t> ref_addrs;
		uint64_t parent_addr;
		std::vector<int> refs;
		int rec_count = 0;
		uint64_t lastAddr = addresses[addr_cnt - 1];

		uint64_t ref_off = 0, ref_off_cnt_max = 1024 * 32;
		std::vector<uint64_t> ref_offsets;
		ref_offsets.reserve(1024 * 32);
		int32_t ref_ndx_cnt_max = 1024 * 64;
		std::vector<int32_t> ref_ndxs;

		try {
			ref_ndxs.reserve(1024 * 64);
			hFile = CreateFile(path_addr_refs, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
			if (!is_valid_handle(hFile)) { return false; }
			hFileOffs = CreateFile(path_fwd_offs, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
			if (!is_valid_handle(hFileOffs)) { return false; }
			hFileRefs = CreateFile(path_fwd_refs, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
			if (!is_valid_handle(hFileRefs)) { return false; }

			size_t off = 0;
			while (count != 0xFFFFFFFF)
			{
				if (!ReadFile(hFile, read_buff + off, read_req - off, &bytes_read, nullptr)) {
					get_last_err();
					return false;
				}

				char* begin = read_buff;
				char* end = read_buff + (bytes_read + off);
				while (begin < end)
				{
					if (read_cnt) {
						if (ref_offsets.size() + count > ref_off_cnt_max) {
							dump_vector<uint64_t>(hFileOffs, ref_offsets);
						}
						if (end - begin < sizeof(uint32_t)) break;
						refs.clear();
						ref_addrs.clear();
						count = *((uint32_t*)begin);
						begin += sizeof(uint32_t);
						ref_offsets.push_back(ref_off);

						if (count == 0xFFFFFFFF) {
							goto EXIT;
						}
						if (count != 0) {
							read_cnt = false;
							if (end - begin < sizeof(uint64_t)) break;
							parent_addr = *((uint64_t*)begin);
							assert(is_lq(parent_addr, lastAddr));
							begin += sizeof(uint64_t);
							--count;
						}
						else {
							read_cnt = true;
							++rec_count;
							continue;
						}
					}

					if (ref_ndxs.size() + count > ref_ndx_cnt_max) {
						dump_vector<int32_t>(hFileRefs, ref_ndxs);
					}

					while (count > 0 && end - begin >= sizeof(uint64_t)) {
						uint64_t addr = *((uint64_t*)begin);
						assert(is_lq(parent_addr, lastAddr));
						begin += sizeof(uint64_t);
						--count;
						ref_addrs.push_back(addr);
					}
					if (count == 0) {
						int parent_ndx = cleanup_fwdrefs(addresses, addr_cnt, parent_addr, ref_addrs, refs);
						handle_fwdrefs(parent_ndx, refs, addresses, bwdcounts, bit_set);
						ref_off += refs.size() * sizeof(int32_t);
						std::sort(refs.begin(), refs.end()); // refrences need to be sorted
						std::copy(refs.begin(), refs.end(), std::back_inserter(ref_ndxs));
						read_cnt = true;
						++rec_count;
					}
					if (end - begin < sizeof(uint64_t)) break;
				} // while (begin < end)

				if (end > begin) {
					off = end - begin;
					memmove(read_buff, begin, off);
				}
				else {
					off = 0;
				}
			}

		EXIT:
			dump_vector<uint64_t>(hFileOffs, ref_offsets);
			dump_vector<int32_t>(hFileRefs, ref_ndxs);

			CloseHandle(hFile);
			CloseHandle(hFileOffs);
			CloseHandle(hFileRefs);
			assert(rec_count == addr_cnt);
			return true;
		}
		catch (const std::exception& e) {
			errors_ << L"EXCEPTION!!! read_fwdrefs" << std::endl << e.what() << std::endl;
			CloseHandle(hFile);
			CloseHandle(hFileOffs);
			CloseHandle(hFileRefs);
			return false;
		}
	}

	static bool read_fwdrefs2(const wchar_t* path_addr_refs, const wchar_t* path_fwd_offs, const wchar_t* path_fwd_refs,
		uint64_t* addresses, int addr_cnt, lbitset& bit_set, int* bwdcounts) {
		bool ok{ true };
		HANDLE hFile = INVALID_HANDLE_VALUE, hFileMap = INVALID_HANDLE_VALUE,
			hFileOffs = INVALID_HANDLE_VALUE, hFileRefs = INVALID_HANDLE_VALUE;
		LPVOID map_ptr{ nullptr };

		DWORD  bytes_read = 0;
		const DWORD read_req = sizeof(uint64_t) * 1024 * 32;
		char   read_buff[read_req];
		bool read_cnt = true;
		uint32_t count{ 0 };
		std::vector<uint64_t> ref_addrs;
		uint64_t parent_addr;
		std::vector<int> refs;
		int rec_count = 0;
		uint64_t lastAddr = addresses[addr_cnt - 1];

		uint64_t ref_off = 0, ref_off_cnt_max = 1024 * 32;
		std::vector<uint64_t> ref_offsets;
		ref_offsets.reserve(1024 * 32);
		int32_t ref_ndx_cnt_max = 1024 * 64;
		std::vector<int32_t> ref_ndxs;

		try {
			ref_ndxs.reserve(1024 * 64);
			size_t off = 0;
			uint64_t* paddr;
			uint8_t* data;

			std::tie(ok, hFile, hFileMap, map_ptr) = get_file_read_mapping(path_addr_refs);
			if (!ok) goto EXIT;
			hFileOffs = CreateFile(path_fwd_offs, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
			if (!is_valid_handle(hFileOffs)) { ok = false; goto EXIT; }
			hFileRefs = CreateFile(path_fwd_refs, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
			if (!is_valid_handle(hFileRefs)) { ok = false; goto EXIT; }

			data = (uint8_t*)map_ptr;
			count = *(int*)data;
			data += sizeof(int);
			ref_offsets.push_back(ref_off);
			while (count != 0xFFFFFFFF)
			{
				++rec_count;
				while (count == 0) {
					if (ref_offsets.size() >= ref_off_cnt_max) {
						dump_vector<uint64_t>(hFileOffs, ref_offsets);
					}
					count = *(int*)data;
					data += sizeof(int);
					ref_offsets.push_back(ref_off);
					++rec_count;
				}
				parent_addr = *(uint64_t*)data;
				assert(is_lq(parent_addr, lastAddr));
				data += sizeof(uint64_t);
				--count;
				if (ref_offsets.size() >= ref_off_cnt_max) {
					dump_vector<uint64_t>(hFileOffs, ref_offsets);
				}
				while (count > 0)
				{
					uint64_t addr = *(uint64_t*)data;
					assert(is_lq(parent_addr, lastAddr));
					data += sizeof(uint64_t);
					ref_addrs.push_back(addr);
					--count;
				}
				if (ref_ndxs.size() + ref_addrs.size() >= ref_ndx_cnt_max) {
					dump_vector<int32_t>(hFileRefs, ref_ndxs);
				}
				int parent_ndx = cleanup_fwdrefs(addresses, addr_cnt, parent_addr, ref_addrs, refs);
				handle_fwdrefs(parent_ndx, refs, addresses, bwdcounts, bit_set);
				ref_off += refs.size() * sizeof(int32_t);
				std::sort(refs.begin(), refs.end()); // refrences need to be sorted
				std::copy(refs.begin(), refs.end(), std::back_inserter(ref_ndxs));
				ref_addrs.clear();
				refs.clear();

				count = *(int*)data;
				data += sizeof(int);
				ref_offsets.push_back(ref_off);
			}

		EXIT:
			dump_vector<uint64_t>(hFileOffs, ref_offsets);
			dump_vector<int32_t>(hFileRefs, ref_ndxs);

			release_file_mapping(hFile, hFileMap, map_ptr);
			CloseHandle(hFileOffs);
			CloseHandle(hFileRefs);
			assert(rec_count == addr_cnt);

			return true;
		}
		catch (const std::exception& e) {
			errors_ << L"EXCEPTION!!! read_fwdrefs" << std::endl << e.what() << std::endl;
			release_file_mapping(hFile, hFileMap, map_ptr);
			CloseHandle(hFileOffs);
			CloseHandle(hFileRefs);
			return false;
		}
	}



	static bool flag_addr_refrerences(const wchar_t* path_fwd_offs,
		const wchar_t* path_fwd_refs,
		uint64_t* addresses,
		int addr_cnt,
		lbitset& bit_set) {
		HANDLE hFileOffs = 0, hFileMapOffs = 0, hFileRefs = 0, hFileMapRefs = 0;
		LPVOID offs_ptr = nullptr, refs_ptr = nullptr;
		bool result;
		std::vector<int> ref_ndxs;
		lbitset done_set(addr_cnt);
		try {
			ref_ndxs.reserve(1024);
			std::tie(result, hFileOffs, hFileMapOffs, offs_ptr) = get_file_read_mapping(path_fwd_offs);
			if (!result) return false;
			std::tie(result, hFileRefs, hFileMapRefs, refs_ptr) = get_file_read_mapping(path_fwd_refs);
			if (!result) return false;

			int iter_count = 0;
			while (bit_set.set_count() > 0) {
				++iter_count;
				int i = bit_set.get_next_set(-1);
				while (i != -1) {
					auto off_cnt = get_offset_and_cnt((uint64_t*)offs_ptr, i);
					get_ref_ndxs((char*)refs_ptr, off_cnt.first, off_cnt.second, ref_ndxs);
					bit_set.reset(i);
					done_set.set(i);
					auto addr = addresses[i];
					auto flag = get_rooted_flag(addr);
					for (int j = 0, jcnt = ref_ndxs.size(); j < jcnt; ++j) {
						auto rndx = ref_ndxs[j];
						if (done_set.is_set(rndx)) {
							bit_set.reset(rndx);
							continue;
						}
						auto raddr = addresses[rndx];
						addresses[rndx] = copy_addr_flag(addr, raddr);
						bit_set.set(rndx);
					}
					i = bit_set.get_next_set(i);
				}
			}

			release_file_mapping(hFileOffs, hFileMapOffs, offs_ptr);
			release_file_mapping(hFileRefs, hFileMapRefs, refs_ptr);
			return true;
		}
		catch (const std::exception& e) {
			errors_ << L"EXCEPTION!!! flag_refrerences" << std::endl << e.what() << std::endl;
			release_file_mapping(hFileOffs, hFileMapOffs, offs_ptr);
			release_file_mapping(hFileRefs, hFileMapRefs, refs_ptr);
			return false;
		}
	}

	static bool build_bwd_refrerences(const wchar_t* path_fwd_offs,
		const wchar_t* path_fwd_refs,
		const wchar_t* path_bwd_offs,
		const wchar_t* path_bwd_refs,
		int addr_cnt,
		int* bwd_counts) {
		HANDLE hBwdRefs = INVALID_HANDLE_VALUE, hBwdRefsMap = INVALID_HANDLE_VALUE,
			hFwdOffs = INVALID_HANDLE_VALUE, hFwdOffsMap = INVALID_HANDLE_VALUE,
			hFwdRefs = INVALID_HANDLE_VALUE, hFwdRefsMap = INVALID_HANDLE_VALUE;
		LPVOID bwd_map_ptr = nullptr, fwd_map_offs_ptr = nullptr, fwd_map_refs_ptr = nullptr;
		bool ok{ true };
		const int off_buf_size{ 2048 };
		const int off_read_size{ off_buf_size * sizeof(uint64_t) };
		int fwdndx{ 0 };

		try {
			std::vector<uint64_t> bwd_offs;
			if (!save_bwref_offsets(path_bwd_offs, addr_cnt, bwd_counts, bwd_offs)) {
				return false;
			}
			if (!create_file_with_size(path_bwd_refs, bwd_offs.back()*sizeof(int32_t))) {
				return false;
			}

			int rec_cnt{ 0 };
			uint64_t* pfwdoffs;
			int32_t* pfwdorefs;
			int32_t* pbwdorefs;
			int32_t fwd_cnt, fwd_ndx;
			uint64_t fwd_off0, fwd_off1, bwd_off;
			std::tie(ok, hFwdOffs, hFwdOffsMap, fwd_map_offs_ptr) = get_file_read_mapping(path_fwd_offs);
			if (!ok) { goto EXIT; }
			std::tie(ok, hFwdRefs, hFwdRefsMap, fwd_map_refs_ptr) = get_file_read_mapping(path_fwd_refs);
			if (!ok) { goto EXIT; }
			std::tie(ok, hBwdRefs, hBwdRefsMap, bwd_map_ptr) = get_file_write_mapping(path_bwd_refs);
			if (!ok) { goto EXIT; }

			pfwdoffs = (uint64_t*)fwd_map_offs_ptr;
			pfwdorefs = (int32_t*)fwd_map_refs_ptr;
			pbwdorefs = (int32_t*)bwd_map_ptr;

			fwd_off0 = *pfwdoffs;
			for (int i = 0; i < addr_cnt; ++i) {
				++pfwdoffs;
				fwd_off1 = *pfwdoffs;
				if (fwd_off1 == fwd_off0) continue;
				assert(fwd_off1 > fwd_off0);
				fwd_cnt = (int)((fwd_off1 - fwd_off0)/sizeof(int32_t));
				pfwdorefs = (int32_t*)((uint8_t*)fwd_map_refs_ptr + fwd_off0);
				fwd_off0 = fwd_off1;
				for (int j = 0; j < fwd_cnt; ++j) {
					fwd_ndx = *pfwdorefs;
					bwd_off = bwd_offs[fwd_ndx];
					pbwdorefs = (int32_t*)((uint8_t*)bwd_map_ptr + bwd_off);
					*pbwdorefs = i;
					bwd_offs[fwd_ndx] += sizeof(int32_t);
					++pfwdorefs;
				}
			}

		EXIT:
			release_file_mapping(hBwdRefs, hBwdRefsMap, bwd_map_ptr);
			release_file_mapping(hFwdRefs, hFwdRefsMap, fwd_map_refs_ptr);
			release_file_mapping(hFwdOffs, hFwdOffsMap, fwd_map_offs_ptr);
			return ok;
		}
		catch (const std::exception& e) {
			release_file_mapping(hBwdRefs, hBwdRefsMap, bwd_map_ptr);
			release_file_mapping(hFwdRefs, hFwdRefsMap, fwd_map_refs_ptr);
			release_file_mapping(hFwdOffs, hFwdOffsMap, fwd_map_offs_ptr);
			errors_ << L"EXCEPTION!!! build_bwd_refrerences" << std::endl << e.what() << std::endl;
			return false;
		}
	}

	static bool save_bwref_offsets(const wchar_t* path_bwd_offs, int addr_cnt, int* bwd_counts, std::vector<uint64_t>& bwd_offs) {
		HANDLE hOffs = INVALID_HANDLE_VALUE;
		try {
			hOffs = CreateFile(path_bwd_offs, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
			if (!is_valid_handle(hOffs)) { return false; }

			bwd_offs.reserve(addr_cnt + 1);
			uint64_t off = 0;
			size_t dump_count = 0;
			bwd_offs.push_back(0);
			for (int i = 0; i < addr_cnt; ++i) {
				if ((bwd_offs.size() % 10000) == 0) {
					auto it = bwd_offs.begin() + dump_count;
					auto dsz = bwd_offs.end() - it;
					if (!dump_tofile(hOffs, &*it, dsz * sizeof(uint64_t))) {
						CloseHandle(hOffs);
						return false;
					}
					dump_count = bwd_offs.size();
				}
				off += bwd_counts[i] * sizeof(int);
				bwd_offs.push_back(off);
			}

			auto it = bwd_offs.begin() + dump_count;
			auto dsz = bwd_offs.end() - it;
			bool ok = dump_tofile(hOffs, &*it, dsz * sizeof(uint64_t));
			CloseHandle(hOffs);
			return ok;
		}
		catch (const std::exception& e) {
			errors_ << L"EXCEPTION!!! save_bwref_offsets" << std::endl << e.what() << std::endl;
			CloseHandle(hOffs);
			return false;
		}
	}

	static std::pair<uint64_t, int> get_offset_and_cnt(uint64_t* ptr, int ndx) {
		uint64_t* start = ptr + ndx;
		++ndx;
		uint64_t* end = ptr + ndx;
		return std::make_pair(*start, static_cast<int>((*end - *start) / sizeof(int32_t)));
	}

	static void get_ref_ndxs(char* ptr, uint64_t off, int cnt, std::vector<int>& vec) {
		vec.clear();
		int* start = (int*)(ptr + off);
		for (int i = 0; i < cnt; ++i, ++start) {
			vec.push_back(*start);
		}
	}

	static bool create_file_with_size(const wchar_t* path, uint64_t sz) {
		HANDLE hFile = CreateFile(path, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
		if (!is_valid_handle(hFile)) { return false; }
		if (!SetFilePointer(hFile, sz, NULL, FILE_BEGIN)) {
			get_last_err();
			CloseHandle(hFile);
			return false;
		}
		if (!SetEndOfFile(hFile)) {
			get_last_err();
			CloseHandle(hFile);
			return false;
		}
		if (!CloseHandle(hFile)) {
			get_last_err();
			CloseHandle(hFile);
			return false;
		}
		return true;
	}

	static bool dump_tofile(HANDLE hFile, LPVOID data, size_t size) {
		if (size < 1) return true;
		DWORD written;
		if (!WriteFile(hFile, data, size, &written, nullptr)) {
			get_last_err();
			return false;
		}
		assert(written == size);
		return true;
	}

	static bool dump_tofile(const wchar_t* path, int count, LPVOID data, size_t size) {
		if (size < 1) return true;
		HANDLE hFile = CreateFile(path, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
		if (!is_valid_handle(hFile)) { return false; }
		DWORD written;
		if (!WriteFile(hFile, &count, sizeof(int), &written, nullptr)) {
			get_last_err();
			CloseHandle(hFile);
			return false;
		}
		assert(written == sizeof(int));
		if (!WriteFile(hFile, data, size, &written, nullptr)) {
			get_last_err();
			CloseHandle(hFile);
			return false;
		}
		assert(written == size);
		CloseHandle(hFile);
		return true;
	}

	static bool read_file(HANDLE hFile, LPVOID data, size_t size) {
		if (size < 1) return true;
		DWORD read;
		if (!ReadFile(hFile, data, size, &read, nullptr)) {
			get_last_err();
			return false;
		}
		assert(read == size);
		return true;
	}

	template<typename NUM>
	static bool dump_vector(HANDLE hFile, std::vector<NUM>& vec) {
		if (vec.size() < 1) return true;
		DWORD written;
		if (!WriteFile(hFile, &*vec.begin(), vec.size() * sizeof(NUM), &written, nullptr)) {
			get_last_err();
			return false;
		}
		assert(written == vec.size() * sizeof(NUM));
		vec.clear();
		return true;
	}

	static bool is_addr_flagged(uint64_t addr) {
		return (addr & FlagMask) != 0;
	}

	static uint64_t get_rooted_flag(uint64_t addr) {
		return (addr & FlagMask) & NotRoot;
	}

	static uint64_t copy_addr_flag(uint64_t source, uint64_t target) {
		if ((source & AddrMask) == (target & AddrMask)) {
			return source | target;
		}
		return target | ((source & FlagMask) & NotRoot);
	}

	static bool is_less(uint64_t addr1, uint64_t addr2) {
		return (addr1 & AddrMask) < (addr2 & AddrMask);
	}
	static bool is_lq(uint64_t addr1, uint64_t addr2) {
		return (addr1 & AddrMask) <= (addr2 & AddrMask);
	}

	static int cleanup_fwdrefs(uint64_t* addresses, int addresses_cnt, uint64_t parent, std::vector<uint64_t>& refs, std::vector<int>& ndxs) {
		ndxs.clear();
		if (parent == 0x000000002ba11020) {
			int a = 1;
		}
		addr_less aless;
		refs.erase(std::remove(refs.begin(), refs.end(), parent), refs.end());
		std::sort(refs.begin(), refs.end(), aless);
		addr_eq aeq;
		refs.erase(std::unique(refs.begin(), refs.end(), aeq), refs.end());
		int pndx = bin_search(addresses, parent, 0, addresses_cnt - 1);
		assert(pndx >= 0);
		int cndx;
		for (auto it = refs.begin(); it != refs.end(); ++it) {
			cndx = bin_search(addresses, *it, 0, addresses_cnt - 1);
			if (cndx >= 0) {
				ndxs.push_back(cndx);
			}
		}
		return pndx;
	}

	static void handle_fwdrefs(int parent, std::vector<int>& refs, uint64_t* addrs, int* bwdcounts, lbitset& bits) {
		uint64_t paddr = addrs[parent];
		bool parent_flagged = is_addr_flagged(paddr);
		bits.reset(parent);
		for (auto it = refs.begin(), ite = refs.end(); it != ite; ++it) {
			int ndx = *it;
			uint64_t addr = addrs[ndx];
			bwdcounts[ndx] += 1;
			if (parent_flagged) {
				addrs[ndx] = copy_addr_flag(paddr, addr);
				if (is_less(addr, paddr))
					bits.set(ndx);
			}
		}
	}

	static int bin_search(uint64_t ary[], uint64_t val, int l, int r) {
		uint64_t v = (val & 0x00FFFFFFFFFFFFFF);
		int m = (l + r) / 2;
		uint64_t mv = (ary[m] & 0x00FFFFFFFFFFFFFF);
		do {
			if (mv == v) return m;
			if (v > mv) l = m + 1;
			else r = m - 1;
			m = (l + r) / 2;
			mv = (ary[m] & 0x00FFFFFFFFFFFFFF);
		} while (r >= l);
		return -l - 1;
	}

	static bool is_sorted(uint64_t ary[], int count) {
		if (count < 2) return true;
		uint64_t prev = (ary[0] & 0x00FFFFFFFFFFFFFF);
		for (int i = 1; i < count; ++i) {
			uint64_t v = (ary[i] & 0x00FFFFFFFFFFFFFF);
			if (prev > v) return false;
			prev = v;
		}
		return true;
	}

	static std::wstring get_file_path(const wchar_t* fileName) {
		std::wstring path;
		wchar_t buf[32];
		_itow_s(ref_builder::runtimeNdx_, buf, 30, 10);
		path.reserve(ref_builder::indexPath_.size() + ref_builder::dumpName_.size() + lstrlenW(fileName) + 16);
		path.append(ref_builder::indexPath_)
			.append(L"\\")
			.append(ref_builder::dumpName_)
			.append(L".")
			.append(fileName)
			.append(L"[")
			.append(buf)
			.append(L"].bin");
		return path;
	}

	static std::tuple<bool, HANDLE, HANDLE, LPVOID> get_file_read_mapping(const wchar_t* path) {
		HANDLE hFile = CreateFile(path, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
		if (!is_valid_handle(hFile)) { return std::tuple<bool, HANDLE, HANDLE, LPVOID>(false, INVALID_HANDLE_VALUE, INVALID_HANDLE_VALUE, NULL); }
		HANDLE hMap = CreateFileMapping(hFile, nullptr, PAGE_READONLY, 0, 0, nullptr);
		if (!is_valid_handle(hMap)) {
			CloseHandle(hFile);
			return std::tuple<bool, HANDLE, HANDLE, LPVOID>(false, INVALID_HANDLE_VALUE, INVALID_HANDLE_VALUE, NULL);
		}
		LPVOID ptr = MapViewOfFile(hMap, FILE_MAP_READ, 0, 0, 0);
		if (!is_valid_ptr(ptr)) {
			CloseHandle(hMap);
			CloseHandle(hFile);
			return std::tuple<bool, HANDLE, HANDLE, LPVOID>(false, INVALID_HANDLE_VALUE, INVALID_HANDLE_VALUE, NULL);
		}
		return std::tuple<bool, HANDLE, HANDLE, LPVOID>(true, hFile, hMap, ptr);
	}

	static std::tuple<bool, HANDLE, HANDLE, LPVOID> get_file_write_mapping(const wchar_t* path) {
		HANDLE hFile = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
		if (!is_valid_handle(hFile)) { return std::tuple<bool, HANDLE, HANDLE, LPVOID>(false, INVALID_HANDLE_VALUE, INVALID_HANDLE_VALUE, NULL); }
		HANDLE hMap = CreateFileMapping(hFile, nullptr, PAGE_READWRITE, 0, 0, nullptr);
		if (!is_valid_handle(hMap)) {
			CloseHandle(hFile);
			return std::tuple<bool, HANDLE, HANDLE, LPVOID>(false, INVALID_HANDLE_VALUE, INVALID_HANDLE_VALUE, NULL);
		}
		LPVOID ptr = MapViewOfFile(hMap, FILE_MAP_WRITE, 0, 0, 0);
		if (!is_valid_ptr(ptr)) {
			CloseHandle(hMap);
			CloseHandle(hFile);
			return std::tuple<bool, HANDLE, HANDLE, LPVOID>(false, INVALID_HANDLE_VALUE, INVALID_HANDLE_VALUE, NULL);
		}
		return std::tuple<bool, HANDLE, HANDLE, LPVOID>(true, hFile, hMap, ptr);
	}

	static void release_file_mapping(HANDLE hFile, HANDLE hMap, LPVOID ptr) {
		UnmapViewOfFile(ptr);
		CloseHandle(hMap);
		CloseHandle(hFile);
	}

	static bool is_valid_handle(HANDLE hnd) {
		LPWSTR pBuffer = NULL;
		if (hnd == INVALID_HANDLE_VALUE) {
			DWORD err = GetLastError();
			FormatMessage(FORMAT_MESSAGE_FROM_SYSTEM |
				FORMAT_MESSAGE_ALLOCATE_BUFFER,
				nullptr,
				err,
				0,
				(LPWSTR)&pBuffer,
				0,
				nullptr);
			errors_ << "[" << err << "] " << pBuffer << std::endl;
			LocalFree(pBuffer);
			return false;
		}
		return true;
	}

	static bool is_valid_ptr(VOID* ptr) {
		LPWSTR pBuffer = NULL;
		if (ptr == NULL) {
			DWORD err = GetLastError();
			FormatMessage(FORMAT_MESSAGE_FROM_SYSTEM |
				FORMAT_MESSAGE_ALLOCATE_BUFFER,
				nullptr,
				err,
				0,
				(LPWSTR)&pBuffer,
				0,
				nullptr);
			errors_ << "[" << err << "] " << pBuffer << std::endl;
			LocalFree(pBuffer);
			return false;
		}
		return true;
	}

	static void get_last_err() {
		LPWSTR pBuffer = NULL;
		DWORD err = GetLastError();
		FormatMessage(FORMAT_MESSAGE_FROM_SYSTEM |
			FORMAT_MESSAGE_ALLOCATE_BUFFER,
			nullptr,
			err,
			0,
			(LPWSTR)&pBuffer,
			0,
			nullptr);
		errors_ << "[" << err << "] " << pBuffer << std::endl;
		LocalFree(pBuffer);
	}

	static size_t addrlen_;
	static uint64_t* addresses_;
	static int* bwdcounts_;
	static int runtimeNdx_;
	static std::wstring dumpName_;
	static std::wstring indexPath_;
	static std::wstringstream errors_;
};

