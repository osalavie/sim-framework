import { registerBusinessMessages } from './src/config/businessMessages'

// 业务语言包在应用入口注册；测试环境沿用同一入口，避免断言落到原始 i18n key。
registerBusinessMessages()
