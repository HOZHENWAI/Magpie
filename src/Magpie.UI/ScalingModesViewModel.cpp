#include "pch.h"
#include "ScalingModesViewModel.h"
#if __has_include("ScalingModesViewModel.g.cpp")
#include "ScalingModesViewModel.g.cpp"
#endif
#include "ScalingModesService.h"
#include "EffectsService.h"
#include "AppSettings.h"
#include "EffectHelper.h"
#include "Logger.h"
#include "StrUtils.h"


using namespace ::Magpie::Core;


namespace winrt::Magpie::UI::implementation {

ScalingModesViewModel::ScalingModesViewModel() {
	std::vector<IInspectable> downscalingEffects;
	downscalingEffects.reserve(7);
	downscalingEffects.push_back(box_value(L"无"));

	_downscalingEffectNames.reserve(6);
	for (const EffectInfo& effectInfo : EffectsService::Get().Effects()) {
		if (effectInfo.IsGenericDownscaler()) {
			_downscalingEffectNames.emplace_back(effectInfo.name,
				StrUtils::ToLowerCase(EffectHelper::GetDisplayName(effectInfo.name)));
		}
	}

	// 根据显示名排序，不区分大小写
	std::sort(_downscalingEffectNames.begin(), _downscalingEffectNames.end(),
		[](const auto& l, const auto& r) { return l.second < r.second; });
	for (const auto& pair : _downscalingEffectNames) {
		downscalingEffects.push_back(box_value(EffectHelper::GetDisplayName(pair.first)));
	}
	_downscalingEffects = single_threaded_vector(std::move(downscalingEffects));

	DownscalingEffect& downscalingEffect = AppSettings::Get().DownscalingEffect();
	if (!downscalingEffect.name.empty()) {
		auto it = std::lower_bound(
			_downscalingEffectNames.begin(),
			_downscalingEffectNames.end(),
			downscalingEffect.name,
			[](const std::pair<std::wstring, std::wstring>& l, const std::wstring& r) { return l.first < r; }
		);

		if (it == _downscalingEffectNames.end() || it->first != downscalingEffect.name) {
			Logger::Get().Warn(fmt::format("降采样效果 {} 不存在",
				StrUtils::UTF16ToUTF8(downscalingEffect.name)));
			downscalingEffect.name.clear();
			downscalingEffect.parameters.clear();
		} else {
			_downscalingEffectIndex = int(it - _downscalingEffectNames.begin() + 1);
		}
	}

	std::vector<IInspectable> scalingModes;
	for (uint32_t i = 0, count = ScalingModesService::Get().GetScalingModeCount(); i < count;++i) {
		scalingModes.push_back(ScalingModeItem(i));
	}
	_scalingModes = single_threaded_observable_vector(std::move(scalingModes));

	_scalingModeMovedRevoker = ScalingModesService::Get().ScalingModeMoved(
		auto_revoke, { this, &ScalingModesViewModel::_ScalingModesService_Moved });
	_scalingModeRemovedRevoker = ScalingModesService::Get().ScalingModeRemoved(
		auto_revoke, { this, &ScalingModesViewModel::_ScalingModesService_Removed });
}

void ScalingModesViewModel::DownscalingEffectIndex(int value) {
	if (_downscalingEffectIndex == value) {
		return;
	}
	_downscalingEffectIndex = value;

	DownscalingEffect& downscalingEffect = AppSettings::Get().DownscalingEffect();
	downscalingEffect.parameters.clear();
	if (value <= 0) {
		downscalingEffect.name.clear();
	} else {
		downscalingEffect.name = _downscalingEffectNames[(size_t)value - 1].first;
	}

	_propertyChangedEvent(*this, PropertyChangedEventArgs(L"DownscalingEffectIndex"));
	_propertyChangedEvent(*this, PropertyChangedEventArgs(L"DownscalingEffectHasParameters"));
	_propertyChangedEvent(*this, PropertyChangedEventArgs(L"DownscalingEffectParameters"));
}

bool ScalingModesViewModel::DownscalingEffectHasParameters() noexcept {
	if (_downscalingEffectIndex == 0) {
		return false;
	}

	const std::wstring& effectName = _downscalingEffectNames[(size_t)_downscalingEffectIndex - 1].first;
	return !EffectsService::Get().GetEffect(effectName)->params.empty();
}

void ScalingModesViewModel::PrepareForAdd() {
	std::vector<IInspectable> copyFromList;
	copyFromList.push_back(box_value(L"无"));
	for (const auto& scalingMode : AppSettings::Get().ScalingModes()) {
		copyFromList.push_back(box_value(scalingMode.name));
	}
	_newScalingModeCopyFromList = single_threaded_vector(std::move(copyFromList));
	_propertyChangedEvent(*this, PropertyChangedEventArgs(L"NewScalingModeCopyFromList"));

	_newScalingModeName.clear();
	_propertyChangedEvent(*this, PropertyChangedEventArgs(L"NewScalingModeName"));

	_newScalingModeCopyFrom = 0;
	_propertyChangedEvent(*this, PropertyChangedEventArgs(L"NewScalingModeCopyFrom"));
}

void ScalingModesViewModel::NewScalingModeName(const hstring& value) noexcept {
	_newScalingModeName = value;
	_propertyChangedEvent(*this, PropertyChangedEventArgs(L"NewScalingModeName"));
	_propertyChangedEvent(*this, PropertyChangedEventArgs(L"IsAddButtonEnabled"));
}

void ScalingModesViewModel::AddScalingMode() {
	ScalingModesService::Get().AddScalingMode(_newScalingModeName, _newScalingModeCopyFrom - 1);
	_scalingModes.Append(ScalingModeItem(ScalingModesService::Get().GetScalingModeCount() - 1));
}

void ScalingModesViewModel::_ScalingModesService_Moved(uint32_t index, bool isMoveUp) {
	uint32_t targetIndex = isMoveUp ? index - 1 : index + 1;

	ScalingModeItem targetItem = _scalingModes.GetAt(targetIndex).as<ScalingModeItem>();
	_scalingModes.RemoveAt(targetIndex);
	_scalingModes.InsertAt(index, targetItem);
}

void ScalingModesViewModel::_ScalingModesService_Removed(uint32_t index) {
	_scalingModes.RemoveAt(index);
}

}
