// refbuilder.cpp : Defines the entry point for the console application.
//

#include "stdafx.h"
#include <string>
#include <iostream>
#include <thread>
#include <chrono>
#include "ref_builder.h"

size_t ref_builder::addrlen_;
uint64_t* ref_builder::addresses_ = nullptr;
int* ref_builder::bwdcounts_ = nullptr;
int ref_builder::runtimeNdx_;
std::wstring ref_builder::dumpName_;
std::wstring ref_builder::indexPath_;
std::wstringstream ref_builder::errors_;

void cleanup() {
	delete ref_builder::addresses_;
	delete ref_builder::bwdcounts_;
}

void out_tm_diff(std::chrono::time_point<std::chrono::steady_clock> start) {
	auto end = std::chrono::steady_clock::now();
	auto diff = end - start;
	std::wcout << static_cast<uint64_t>(std::chrono::duration<double, std::milli>(diff).count() / 1000) << L" sec" << std::endl;
}

int wmain(int argc, wchar_t *argv[])
{
	try {
		bool ok{ true };
		ref_builder::runtimeNdx_ = _wtoi(argv[1]);
		ref_builder::dumpName_.assign(argv[2]);
		ref_builder::indexPath_.assign(argv[3]);
		wchar_t diamond = static_cast<wchar_t>(0x2756);

		std::wcout << L"~REFBUILDER " << ref_builder::indexPath_ << std::endl;
		std::wcout << L"~REFBUILDER reading addresses ";
		std::chrono::time_point<std::chrono::steady_clock> start = std::chrono::steady_clock::now();

		out_tm_diff(start);

		std::wcout << L"~REFBUILDER generating forward references ";
		start = std::chrono::steady_clock::now();

		out_tm_diff(start);


		std::wcout << L"~REFBUILDER flagging forward reference addresses ";
		start = std::chrono::steady_clock::now();

		out_tm_diff(start);

		std::wcout << L"~REFBUILDER building backward references ";
		start = std::chrono::steady_clock::now();

		out_tm_diff(start);

		//std::this_thread::sleep_for(std::chrono::milliseconds(10000));

	EXIT:
		cleanup();
		if (!ok) { std::wcout << ref_builder::errors_.str() << std::endl; }
		return ok ? 0 : -1;
	}
	catch (const std::exception& e) {
		std::wcout << L"REFBUILDER  EXCEPTION!!!" << std::endl << e.what() << std::endl;
		cleanup();
		return -1;
	}


    return 0;
}

