// 移植自 https://github.com/bloc97/Anime4K/blob/master/glsl/Denoise/Anime4K_Denoise_Bilateral_Mean.glsl


//!MAGPIE EFFECT
//!VERSION 2
//!OUTPUT_WIDTH INPUT_WIDTH
//!OUTPUT_HEIGHT INPUT_HEIGHT


//!PARAMETER
//!MIN 1e-5
//!DEFAULT 0.1
float intensitySigma;

//!TEXTURE
Texture2D INPUT;

//!SAMPLER
//!FILTER POINT
SamplerState sam;


//!PASS 1
//!IN INPUT
//!BLOCK_SIZE 8,8
//!NUM_THREADS 64,1,1

#define INTENSITY_SIGMA intensitySigma //Intensity window size, higher is stronger denoise, must be a positive real number
#define SPATIAL_SIGMA 1.0 //Spatial window size, higher is stronger denoise, must be a positive real number.

#define INTENSITY_POWER_CURVE 1.0 //Intensity window power curve. Setting it to 0 will make the intensity window treat all intensities equally, while increasing it will make the window narrower in darker intensities and wider in brighter intensities.

#define KERNELSIZE (max(uint(ceil(SPATIAL_SIGMA * 2.0)), 1) * 2 + 1) //Kernel size, must be an positive odd integer.
#define KERNELHALFSIZE (int(KERNELSIZE/2)) //Half of the kernel size without remainder. Must be equal to trunc(KERNELSIZE/2).
#define KERNELLEN (KERNELSIZE * KERNELSIZE) //Total area of kernel. Must be equal to KERNELSIZE * KERNELSIZE.

#define GETOFFSET(i) int2(int(i % KERNELSIZE) - KERNELHALFSIZE, int(i / KERNELSIZE) - KERNELHALFSIZE)

float3 gaussian_vec(float3 x, float3 s, float3 m) {
	float3 scaled = (x - m) / s;
	return exp(-0.5 * scaled * scaled);
}

float gaussian(float x, float s, float m) {
	float scaled = (x - m) / s;
	return exp(-0.5 * scaled * scaled);
}


void Pass1(uint2 blockStart, uint3 threadId) {
	uint2 gxy = Rmp8x8(threadId.x) + blockStart;
	if (!CheckViewport(gxy)) {
		return;
	}

	float2 inputPt = GetInputPt();
	float2 pos = (gxy + 0.5f) * inputPt;

	float3 sum = 0;
	float3 n = 0;

	float3 vc = INPUT.SampleLevel(sam, pos, 0).rgb;

	float3 is = pow(vc + 0.0001, INTENSITY_POWER_CURVE) * INTENSITY_SIGMA;
	float ss = SPATIAL_SIGMA;

	[unroll]
	for (uint i = 0; i < KERNELLEN; i++) {
		float2 ipos = pos + GETOFFSET(i) * inputPt;
		float3 v = INPUT.SampleLevel(sam, ipos, 0).rgb;
		float3 d = gaussian_vec(v, is, vc) * gaussian(length(ipos), ss, 0.0);
		sum += d * v;
		n += d;
	}

	WriteToOutput(gxy, sum / n);
}
