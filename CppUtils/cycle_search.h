#pragma once
#include "stdafx.h"
#include <boost/graph/graph_traits.hpp>
#include <boost/graph/adjacency_list.hpp>
#include <boost/graph/dijkstra_shortest_paths.hpp>
#include <boost/graph/hawick_circuits.hpp>

namespace bldutl {
	using namespace std;
	using namespace boost;


	typedef adjacency_list<vecS, vecS, directedS> Graph;

	struct cycle_visitor {

		cycle_visitor(vector<vector<int>>* pcycle_list) : _pcycle_list(pcycle_list) {

		}

		template <typename Path, typename Graph>
		void cycle(Path const& p, Graph const& g) {

			typedef typename graph_traits<Graph>::vertex_descriptor Vertex;

			typedef typename graph_traits<Graph>::edge_descriptor Edge;

			typename Path::const_iterator i;
			std::vector<int> vec;
			for (i = p.begin(); i != p.end(); ++i) {
				vec.push_back(*i);
			}
			_pcycle_list->push_back(vec);
		}

		vector<vector<int>>* _pcycle_list;
	};

	struct graph {

		void init_graph(size_t vertex_count, vector<pair<int, int>>& edges) {
			vector<boost::adjacency_list<>::vertex_descriptor> vertices(vertex_count);
			for (int i = 0; i < vertex_count; ++i)
				vertices.push_back(boost::add_vertex(_graph));
			for (auto it = edges.begin(); it != edges.end(); ++it) {
				boost::add_edge(it->first, it->second, _graph);
			}
		}

		void init_graph(size_t vertex_count) {
			for (int i = 0; i < vertex_count; ++i)
				boost::add_vertex(_graph);
		}

		void add_edge(int i, int j) {
			boost::add_edge(i, j, _graph);
		}

		vector<vector<int>> get_cycles() {
			vector<vector<int>> cycle_list;
			cycle_visitor visitor(&cycle_list);
			boost::hawick_circuits(_graph, visitor);
			return cycle_list;
		}

		Graph _graph;
	};
}

