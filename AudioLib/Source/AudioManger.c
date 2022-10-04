#include "AudioManager.h"

float GetAudioTime(const char* FilePath, int* error) {
	stb_vorbis* VorbisData = stb_vorbis_open_filename(FilePath, error, NULL);
	float Length = -1.0f;
	if(*error == 0) {
		Length = stb_vorbis_stream_length_in_seconds(VorbisData);
	}
	stb_vorbis_close(VorbisData);
	return Length;
}