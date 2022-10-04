#pragma once
#include "stb_vorbis.h"

#ifdef BUILD_DLL
	#define EXPORT_API __declspec(dllexport)
#else
	#define EXPORT_API __declspec(dllimport)
#endif

float EXPORT_API GetAudioTime(const char* FilePath, int* error);