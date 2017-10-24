// CppUtils.h

#pragma once
#include "Stdafx.h"
#include "cycle_search.h"

using namespace System;

namespace CppUtils {

	public ref class GraphHelper
	{

	public:
		array<array<int>^>^ Cycles;
		GraphHelper() {
			Cycles = nullptr;
			_pgraph = nullptr;
		}

	private:
		bldutl::graph* _pgraph;

	public:
		
		void Init(int count, array<System::Collections::Generic::List<int>^>^ graph) {
			_pgraph = new bldutl::graph();
			_pgraph->init_graph(graph->Length);
			for (int i = 0, icnt = graph->Length; i < icnt; ++i) {
				System::Collections::Generic::List<int>^ lst = graph[i];
				if (lst == nullptr) continue;
				for (int j = 0, jcnt = lst->Count; j < jcnt; ++j) {
					_pgraph->add_edge(i, lst[j]);
				}
			}
		}

		bool GetCycles() {
			bool result = false;
			std::vector<std::vector<int>> cycles = _pgraph->get_cycles();
			if (cycles.size() > 0) {
				result = true;
				Cycles = gcnew array<array<int>^>(cycles.size());
				int ndx = 0;
				for (std::vector<std::vector<int>>::const_iterator it = cycles.begin(); it != cycles.end(); ++it) {
					if (it->size() == 0) continue;
					Cycles[ndx] = gcnew array<int>(it->size());
					int k = 0;
					for (std::vector<int>::const_iterator i = it->begin(); i != it->end(); ++i) {
						Cycles[ndx][k++] = *i;
					}
					++ndx;
				}
			}
			return result;
		}

		bool HasCycles() {
			return Cycles != nullptr && Cycles->Length > 0;
		}

		array<array<int>^>^ GetCycleArrays() {
			return Cycles;
		}

		~GraphHelper() {
			this->!GraphHelper();
		}

		!GraphHelper() {
			if (_pgraph != nullptr) free(_pgraph);
		}
		// TODO: Add your methods for this class here.
	};
}
