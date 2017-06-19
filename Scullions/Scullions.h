// Scullions.h

#pragma once

using namespace System;
using namespace System::Collections::Generic;
using namespace System::Collections::Concurrent;
using namespace cli;
#include "reference_handler.h"

void* open_write_file(std::wstring& path);
void  write_file(void* hnd, __int8* bytes, int len);
void  read_file(void* hnd, void* bytes, int len);
void* open_read_file(std::wstring& path);
void close_file(void* hnd);

namespace Scullions {

	public ref class ReferenceBuilder
	{
		const int INVALID_INSTANCE = -1;
		unsigned long long *_instances;
		int _inst_count;
		String^ _outFolder;
		int _reflag_count;
		int _not_found_count;
		int _reversed_min;
		int _reversed_max;

		BlockingCollection<KeyValuePair<System::Int32, cli::array<System::UInt64>^>>^ _queue;

	public: ReferenceBuilder(BlockingCollection<KeyValuePair<System::Int32, cli::array<System::UInt64>^>>^ queue, String^ outFolder)
	{
		_queue = queue;
		_outFolder = outFolder;
	}

	public: ReferenceBuilder(String^ outFolder)
	{
		_queue = nullptr;
		_outFolder = outFolder;
	}

	public: void Init(cli::array<System::UInt64>^ instances) {
		pin_ptr<System::UInt64> pinst = &instances[0];
		_inst_count = instances->Length;
		_instances = new unsigned long long[_inst_count];
		for (int i = 0, icnt = _inst_count; i < icnt; ++i) {
			_instances[i] = instances[i];
		}
		pinst = nullptr;
	}

	public: int GetReflagCount() {
		return _reflag_count;
	}

	public: int GetNotFoundCount() {
		return _not_found_count;
	}

	public: KeyValuePair<int,int> GetReversedMinMax() {
		return KeyValuePair<int,int>(_reversed_min, _reversed_max);
	}

	public: void Work()
	{
		IntPtr p = Runtime::InteropServices::Marshal::StringToHGlobalUni(_outFolder);
		wchar_t *pNewCharStr = static_cast<wchar_t*>(p.ToPointer());
		std::wstring out_foldder(pNewCharStr);
		Runtime::InteropServices::Marshal::FreeHGlobal(p);

		reference_handler refhndl(_instances, _inst_count, out_foldder.c_str());
		std::vector<unsigned long long> children(1024);
		std::vector<int> indices(1024);

		void* hndFwdRefs = open_write_file(out_foldder + L"\\ForwardRefs.bin");
		void* hndFwdOffs = open_write_file(out_foldder + L"\\ForwardOffs.bin");
		long long offset=0;

		while (true)
		{
			KeyValuePair<System::Int32, cli::array<System::UInt64>^>^ kv = _queue->Take();
			if (kv->Key == INVALID_INSTANCE) {
				write_file(hndFwdOffs, (__int8*)&offset, (int)sizeof(long long));
				break;
			}
			
			cli::array<System::UInt64>^ ary = kv->Value;
			int len = ary->Length;
			if (len == 0) {
				write_file(hndFwdOffs, (__int8*)&offset, (int)sizeof(long long));
				continue;
			}
			pin_ptr<System::UInt64> pp = &ary[0];
			children.clear();
			children.reserve(len);
			unsigned long long parent = ary[0];
			for (int i = 1; i < len; ++i) {
				unsigned long long inst = ary[i];
				children.push_back(inst);
			}
			pp = nullptr;
			unsigned long long* dataend = refhndl.preprocess_parent_refs(children);
			refhndl.process_parent_refs(parent, kv->Key, &(*children.begin()), dataend, indices);

			write_file(hndFwdOffs, (__int8*)&offset, (int)sizeof(long long));
			if (indices.size() < 1) continue;
			long long len_to_write = (long long)(indices.size() * sizeof(unsigned long long));
			offset += len_to_write;
			write_file(hndFwdRefs, (__int8*)&(indices[0]), (int)len_to_write);

		}
		close_file(hndFwdRefs);
		close_file(hndFwdOffs);

		// extra info
		_reflag_count = refhndl.get_reflag_count();
		_not_found_count = refhndl.get_notfound_count();
		std::pair<int, int> rminmax = refhndl.get_reversed_minmax();
		_reversed_min = rminmax.first;
		_reversed_max = rminmax.second;
	}

	public: void Work2()
	{
		IntPtr p = Runtime::InteropServices::Marshal::StringToHGlobalUni(_outFolder);
		wchar_t *pNewCharStr = static_cast<wchar_t*>(p.ToPointer());
		std::wstring out_foldder(pNewCharStr);
		Runtime::InteropServices::Marshal::FreeHGlobal(p);

		reference_handler refhndl(_instances, _inst_count, out_foldder.c_str());
		std::vector<unsigned long long> children(1024);
		std::vector<int> indices(1024);

		void* hndFwdRefs = open_write_file(out_foldder + L"\\ForwardRefs.bin");
		void* hndFwdOffs = open_write_file(out_foldder + L"\\ForwardOffs.bin");
		//void* hndBwdCounts = open_write_file(out_foldder + L"\\BackwardCounts.bin");
		void* hndFwdData = open_read_file(out_foldder + L"\\refsdata.bin");
		long long offset = 0;
		int dbuffer_size = 1024;
		unsigned long long *dbuffer = new unsigned long long[dbuffer_size];

		for (int i = 0, icnt = _inst_count; i < icnt; ++i)
		{
			int dcount = 0;
			read_file(hndFwdData, &dcount, 4);
			if (dcount == 0) {
				write_file(hndFwdOffs, (__int8*)&offset, (int)sizeof(long long));
				continue;
			}

			if (dbuffer_size < dcount) {
				dbuffer_size = dcount;
				dbuffer = new unsigned long long[dbuffer_size];
			}
			read_file(hndFwdData, dbuffer, dcount*(int)sizeof(unsigned long long));
			unsigned long long parent = *dbuffer;
			unsigned long long* dataend = refhndl.preprocess_parent_refs(dbuffer, dbuffer + dcount);
			refhndl.process_parent_refs(parent, i, dbuffer, dataend, indices);

			write_file(hndFwdOffs, (__int8*)&offset, (int)sizeof(long long));
			if (indices.size() < 1) continue;
			long long len_to_write = (long long)(indices.size() * sizeof(unsigned long long));
			offset += len_to_write;
			write_file(hndFwdRefs, (__int8*)&(indices[0]), (int)len_to_write);

		}
		close_file(hndFwdData);
		close_file(hndFwdRefs);
		close_file(hndFwdOffs);

		_reflag_count = refhndl.get_reflag_count();
		_not_found_count = refhndl.get_notfound_count();
		std::pair<int, int> rminmax = refhndl.get_reversed_minmax();
		_reversed_min = rminmax.first;
		_reversed_max = rminmax.second;



	}
			// TODO: Add your methods for this class here.
	};
}
