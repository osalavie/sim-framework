/**
 * 主题风格注册表
 *
 * 这里是多主题色值的唯一来源：
 * - 每套风格包含「月白 / 玄夜」两份调色盘；
 * - `paletteToCssVars` 把调色盘展开为运行时 `--po-*` 令牌；
 * - `paletteToThemeColors` 提供 Naive UI、`useTheme` 与 `--theme-*` 需要的语义色；
 * - 纸上海洋的取值与 `styles/theme-variables.css` 默认值逐项一致，保证存量视觉不变。
 *
 * 颜色必须是 hex 或 rgba 字面量：Naive UI 需要自行派生 hover / pressed，
 * 不能接收 CSS 变量或 color-mix()。
 */

export type ThemeStyleId = 'paper-ocean' | 'xuanqing' | 'zhuyue' | 'zhuxiang'
/** 用户可选的明暗模式；system 表示跟随操作系统。 */
export type ThemeMode = 'light' | 'dark' | 'system'
export type ThemeSkin = 'paper' | 'business'

/** 供 Naive UI、useTheme 与 --theme-* 使用的语义色。 */
export interface ThemeColors {
  primary: string
  primaryHover: string
  primaryPressed: string
  info: string
  success: string
  warning: string
  error: string
  textBase: string
  textSecondary: string
  textTertiary: string
  textDisabled: string
  bgBase: string
  bgSecondary: string
  bgTertiary: string
  border: string
  borderHover: string
  divider: string
}

export interface ThemePalette {
  /* 表面与文字 */
  bg: string
  bgDeep: string
  paper: string
  raised: string
  ink: string
  inkSoft: string
  inkFaint: string
  disabled: string
  line: string
  lineMid: string
  edge: string
  edgeStrong: string
  /* 主色与交互 */
  primary: string
  primaryHover: string
  primaryPressed: string
  primarySoft: string
  primarySoftHover: string
  primarySoftPressed: string
  onPrimary: string
  accent: string
  accentHover: string
  accentPressed: string
  accentSoft: string
  /* 装饰别名（映射存量 --po-coral / --po-sun 等） */
  coral: string
  coralHover: string
  coralDeep: string
  /** 珊瑚底上的文字与图形；纸上海洋用墨色，商务风格的珊瑚即主色，用 onPrimary。 */
  onCoral: string
  sun: string
  sunSoft: string
  sky: string
  peach: string
  mint: string
  lilac: string
  dot: string
  scrim: string
  /* 语义色 */
  success: string
  successSoft: string
  warning: string
  warningSoft: string
  danger: string
  dangerSoft: string
  info: string
  infoSoft: string
}

export interface ThemeStyleDefinition {
  id: ThemeStyleId
  skin: ThemeSkin
  /** 常驻 account 语言包中的名称键 */
  labelKey: string
  /** 常驻 account 语言包中的短描述键 */
  descriptionKey: string
  /** 设置页风格卡的三段预览色 */
  preview: readonly [string, string, string]
  light: ThemePalette
  dark: ThemePalette
}

const paperOceanLight: ThemePalette = {
  bg: '#fff9ee',
  bgDeep: '#f9f2e3',
  paper: '#ffffff',
  raised: '#ffffff',
  ink: '#14284b',
  inkSoft: '#4f6282',
  inkFaint: '#70819f',
  disabled: '#a8b4c9',
  line: '#d9e2f1',
  lineMid: '#c0cde2',
  edge: '#14284b',
  edgeStrong: '#14284b',
  primary: '#2f6bff',
  primaryHover: '#1f56e0',
  primaryPressed: '#1b4bd1',
  primarySoft: '#e7f1ff',
  primarySoftHover: '#dcebff',
  primarySoftPressed: '#c7dbff',
  onPrimary: '#ffffff',
  accent: '#2f6bff',
  accentHover: '#1b4fd6',
  accentPressed: '#153fae',
  accentSoft: '#e7f1ff',
  coral: '#ff7a6b',
  coralHover: '#ff9084',
  coralDeep: '#da4339',
  onCoral: '#14284b',
  sun: '#ffc94d',
  sunSoft: '#fff0b8',
  sky: '#dcebff',
  peach: '#ffe3d6',
  mint: '#bff0d8',
  lilac: '#e6dcff',
  dot: 'rgba(20, 40, 75, 0.13)',
  scrim: 'rgba(20, 40, 75, 0.34)',
  success: '#018451',
  successSoft: '#bff0d8',
  warning: '#a56d1c',
  warningSoft: '#ffc94d',
  danger: '#c1332f',
  dangerSoft: '#ffe3d6',
  info: '#2f6bff',
  infoSoft: '#dcebff',
}

const paperOceanDark: ThemePalette = {
  bg: '#121a2b',
  bgDeep: '#182236',
  paper: '#1c2740',
  raised: '#22304d',
  ink: '#f4eee1',
  inkSoft: '#aab5cb',
  inkFaint: '#8492ad',
  disabled: '#5b6982',
  line: '#33415f',
  lineMid: '#465774',
  edge: '#f4eee1',
  edgeStrong: '#f4eee1',
  primary: '#7ea2ff',
  primaryHover: '#9db9ff',
  primaryPressed: '#6b93ff',
  primarySoft: '#22304d',
  primarySoftHover: '#263f5e',
  primarySoftPressed: '#2f4b70',
  onPrimary: '#14284b',
  accent: '#7ea2ff',
  accentHover: '#9db9ff',
  accentPressed: '#6b93ff',
  accentSoft: '#22304d',
  coral: '#ff8b79',
  coralHover: '#ffa595',
  coralDeep: '#ff6b57',
  onCoral: '#14284b',
  sun: '#f6d06a',
  sunSoft: '#3b3320',
  sky: '#263f5e',
  peach: '#4a2f2b',
  mint: '#203c32',
  lilac: '#312c4f',
  dot: 'rgba(244, 238, 225, 0.07)',
  scrim: 'rgba(6, 10, 18, 0.55)',
  success: '#5fd39a',
  successSoft: '#203c32',
  warning: '#e0b264',
  warningSoft: '#3b3320',
  danger: '#ff9186',
  dangerSoft: '#4a2f2b',
  info: '#7ea2ff',
  infoSoft: '#263f5e',
}

const xuanqingLight: ThemePalette = {
  bg: '#f5f6f4',
  bgDeep: '#eceff0',
  paper: '#ffffff',
  raised: '#ffffff',
  ink: '#1b2a33',
  inkSoft: '#52636d',
  inkFaint: '#5f6e78',
  disabled: '#9aa7ad',
  line: '#dde4e1',
  lineMid: '#c4d0cc',
  edge: '#cfd8d5',
  edgeStrong: '#a9b9b3',
  primary: '#2e5065',
  primaryHover: '#264456',
  primaryPressed: '#1e3848',
  primarySoft: '#e8eff3',
  primarySoftHover: '#dde8ee',
  primarySoftPressed: '#d2e0e8',
  onPrimary: '#f7f6f1',
  accent: '#2e5065',
  accentHover: '#264456',
  accentPressed: '#1e3848',
  accentSoft: '#e8eff3',
  coral: '#2e5065',
  coralHover: '#264456',
  coralDeep: '#1e3848',
  onCoral: '#f7f6f1',
  sun: '#d2b36a',
  sunSoft: '#f4eddd',
  sky: '#e8eff3',
  peach: '#f8e7e4',
  mint: '#e6f2ec',
  lilac: '#f4eddd',
  dot: 'transparent',
  scrim: 'rgba(15, 22, 27, 0.42)',
  success: '#2e7d5b',
  successSoft: '#e6f2ec',
  warning: '#9a6a18',
  warningSoft: '#f7edd9',
  danger: '#af3a2e',
  dangerSoft: '#f8e7e4',
  info: '#376c8b',
  infoSoft: '#e8f0f4',
}

const xuanqingDark: ThemePalette = {
  bg: '#0f161b',
  bgDeep: '#141e24',
  paper: '#19242b',
  raised: '#202e36',
  ink: '#edf0ec',
  inkSoft: '#a7b4ba',
  inkFaint: '#7d8b92',
  disabled: '#55636a',
  line: '#2a3a42',
  lineMid: '#36464e',
  edge: '#36464e',
  edgeStrong: '#4e626b',
  primary: '#8fb6cf',
  primaryHover: '#a6c7db',
  primaryPressed: '#79a2bd',
  primarySoft: '#23333c',
  primarySoftHover: '#2a3d47',
  primarySoftPressed: '#314751',
  onPrimary: '#0f161b',
  accent: '#8fb6cf',
  accentHover: '#a6c7db',
  accentPressed: '#79a2bd',
  accentSoft: '#23333c',
  coral: '#8fb6cf',
  coralHover: '#a6c7db',
  coralDeep: '#79a2bd',
  onCoral: '#0f161b',
  sun: '#d2b36a',
  sunSoft: '#332c1d',
  sky: '#23333c',
  peach: '#3a2321',
  mint: '#1c3329',
  lilac: '#332c1d',
  dot: 'transparent',
  scrim: 'rgba(0, 0, 0, 0.6)',
  success: '#6fc29a',
  successSoft: '#1c3329',
  warning: '#e0b264',
  warningSoft: '#332c1d',
  danger: '#e88378',
  dangerSoft: '#3a2321',
  info: '#8ab8d2',
  infoSoft: '#1e2f38',
}

const zhuyueLight: ThemePalette = {
  bg: '#f3f6f3',
  bgDeep: '#e9efeb',
  paper: '#ffffff',
  raised: '#ffffff',
  ink: '#1f2c27',
  inkSoft: '#52645c',
  inkFaint: '#64766e',
  disabled: '#9daca5',
  line: '#dce5df',
  lineMid: '#c3d3c9',
  edge: '#ccd9d2',
  edgeStrong: '#a6bbaf',
  primary: '#3e6b57',
  primaryHover: '#345a49',
  primaryPressed: '#2a4a3c',
  primarySoft: '#e6f0ea',
  primarySoftHover: '#dae9e0',
  primarySoftPressed: '#cee2d6',
  onPrimary: '#f7f8f4',
  accent: '#3e6b57',
  accentHover: '#345a49',
  accentPressed: '#2a4a3c',
  accentSoft: '#e6f0ea',
  coral: '#3e6b57',
  coralHover: '#345a49',
  coralDeep: '#2a4a3c',
  onCoral: '#f7f8f4',
  sun: '#8fb6a6',
  sunSoft: '#e4efea',
  sky: '#e6f0ea',
  peach: '#f8e7e4',
  mint: '#e6f2ec',
  lilac: '#e4efea',
  dot: 'transparent',
  scrim: 'rgba(15, 22, 19, 0.42)',
  success: '#2e7d5b',
  successSoft: '#e6f2ec',
  warning: '#9a6a18',
  warningSoft: '#f7edd9',
  danger: '#af3a2e',
  dangerSoft: '#f8e7e4',
  info: '#376c8b',
  infoSoft: '#e8f0f4',
}

const zhuyueDark: ThemePalette = {
  bg: '#0e1512',
  bgDeep: '#131c18',
  paper: '#19231f',
  raised: '#202c27',
  ink: '#e9efea',
  inkSoft: '#a6b4ac',
  inkFaint: '#7c8c83',
  disabled: '#54645c',
  line: '#293a33',
  lineMid: '#35463e',
  edge: '#35463e',
  edgeStrong: '#4a5e54',
  primary: '#8fc0a6',
  primaryHover: '#a5d0b9',
  primaryPressed: '#79ac92',
  primarySoft: '#23342c',
  primarySoftHover: '#2a3e35',
  primarySoftPressed: '#31483d',
  onPrimary: '#0e1512',
  accent: '#8fc0a6',
  accentHover: '#a5d0b9',
  accentPressed: '#79ac92',
  accentSoft: '#23342c',
  coral: '#8fc0a6',
  coralHover: '#a5d0b9',
  coralDeep: '#79ac92',
  onCoral: '#0e1512',
  sun: '#a3c9ba',
  sunSoft: '#20312a',
  sky: '#23342c',
  peach: '#3a2321',
  mint: '#1c3329',
  lilac: '#20312a',
  dot: 'transparent',
  scrim: 'rgba(0, 0, 0, 0.6)',
  success: '#6fc29a',
  successSoft: '#1c3329',
  warning: '#e0b264',
  warningSoft: '#332c1d',
  danger: '#e88378',
  dangerSoft: '#3a2321',
  info: '#8ab8d2',
  infoSoft: '#1e2f38',
}

const zhuxiangLight: ThemePalette = {
  bg: '#f8f4ea',
  bgDeep: '#f1eadb',
  paper: '#fffdf8',
  raised: '#fffdf8',
  ink: '#2b2420',
  inkSoft: '#675b52',
  inkFaint: '#786b61',
  disabled: '#a89b90',
  line: '#e2d8c6',
  lineMid: '#d0bfa5',
  edge: '#d8cab2',
  edgeStrong: '#bfa98c',
  primary: '#a93b2e',
  primaryHover: '#8f3025',
  primaryPressed: '#75271e',
  primarySoft: '#f7e7e2',
  primarySoftHover: '#f1dcd5',
  primarySoftPressed: '#ebd1c8',
  onPrimary: '#fff8ef',
  accent: '#a93b2e',
  accentHover: '#8f3025',
  accentPressed: '#75271e',
  accentSoft: '#f7e7e2',
  coral: '#a93b2e',
  coralHover: '#8f3025',
  coralDeep: '#75271e',
  onCoral: '#fff8ef',
  sun: '#d9b44a',
  sunSoft: '#f6edcf',
  sky: '#f7e7e2',
  peach: '#f4e4e6',
  mint: '#e6f2ec',
  lilac: '#f6edcf',
  dot: 'transparent',
  scrim: 'rgba(24, 16, 12, 0.42)',
  success: '#2e7d5b',
  successSoft: '#e6f2ec',
  warning: '#9a6a18',
  warningSoft: '#f7edd9',
  danger: '#8c3a42',
  dangerSoft: '#f4e4e6',
  info: '#376c8b',
  infoSoft: '#e8f0f4',
}

const zhuxiangDark: ThemePalette = {
  bg: '#151110',
  bgDeep: '#1b1614',
  paper: '#221c19',
  raised: '#2a231f',
  ink: '#f2ebdf',
  inkSoft: '#b8aba0',
  inkFaint: '#8f8278',
  disabled: '#5f544c',
  line: '#322924',
  lineMid: '#433830',
  edge: '#433830',
  edgeStrong: '#5c4b40',
  primary: '#e89a8d',
  primaryHover: '#f0b0a5',
  primaryPressed: '#da887a',
  primarySoft: '#3a2521',
  primarySoftHover: '#432b26',
  primarySoftPressed: '#4b312b',
  onPrimary: '#151110',
  accent: '#e89a8d',
  accentHover: '#f0b0a5',
  accentPressed: '#da887a',
  accentSoft: '#3a2521',
  coral: '#e89a8d',
  coralHover: '#f0b0a5',
  coralDeep: '#da887a',
  onCoral: '#151110',
  sun: '#e6c76a',
  sunSoft: '#352d1c',
  sky: '#3a2521',
  peach: '#3a2325',
  mint: '#1c3329',
  lilac: '#352d1c',
  dot: 'transparent',
  scrim: 'rgba(0, 0, 0, 0.62)',
  success: '#6fc29a',
  successSoft: '#1c3329',
  warning: '#e0b264',
  warningSoft: '#332c1d',
  danger: '#e5a0a8',
  dangerSoft: '#3a2325',
  info: '#8ab8d2',
  infoSoft: '#1e2f38',
}

export const themeStyles: readonly ThemeStyleDefinition[] = [
  {
    id: 'paper-ocean',
    skin: 'paper',
    labelKey: 'Theme:PaperOcean',
    descriptionKey: 'Theme:PaperOceanDesc',
    preview: ['#fff9ee', '#14284b', '#ff7a6b'],
    light: paperOceanLight,
    dark: paperOceanDark,
  },
  {
    id: 'xuanqing',
    skin: 'business',
    labelKey: 'Theme:Xuanqing',
    descriptionKey: 'Theme:XuanqingDesc',
    preview: ['#f5f6f4', '#2e5065', '#d2b36a'],
    light: xuanqingLight,
    dark: xuanqingDark,
  },
  {
    id: 'zhuyue',
    skin: 'business',
    labelKey: 'Theme:Zhuyue',
    descriptionKey: 'Theme:ZhuyueDesc',
    preview: ['#f3f6f3', '#3e6b57', '#8fb6a6'],
    light: zhuyueLight,
    dark: zhuyueDark,
  },
  {
    id: 'zhuxiang',
    skin: 'business',
    labelKey: 'Theme:Zhuxiang',
    descriptionKey: 'Theme:ZhuxiangDesc',
    preview: ['#f8f4ea', '#a93b2e', '#d9b44a'],
    light: zhuxiangLight,
    dark: zhuxiangDark,
  },
]

export const DEFAULT_THEME_STYLE_ID: ThemeStyleId = 'paper-ocean'

const themeStyleMap = new Map<ThemeStyleId, ThemeStyleDefinition>(
  themeStyles.map((style) => [style.id, style]),
)

/** 读取风格定义；未知或脏数据回落到纸上海洋。 */
export function getThemeStyle(id: string | null | undefined): ThemeStyleDefinition {
  return themeStyleMap.get(id as ThemeStyleId) ?? themeStyleMap.get(DEFAULT_THEME_STYLE_ID)!
}

export function getThemePalette(styleId: string | null | undefined, dark: boolean): ThemePalette {
  const style = getThemeStyle(styleId)
  return dark ? style.dark : style.light
}

/** 供 Naive UI、useTheme 与 --theme-* 使用的语义色。 */
export function paletteToThemeColors(palette: ThemePalette): ThemeColors {
  return {
    primary: palette.primary,
    primaryHover: palette.primaryHover,
    primaryPressed: palette.primaryPressed,
    info: palette.info,
    success: palette.success,
    warning: palette.warning,
    error: palette.danger,
    textBase: palette.ink,
    textSecondary: palette.inkSoft,
    textTertiary: palette.inkFaint,
    textDisabled: palette.disabled,
    bgBase: palette.paper,
    bgSecondary: palette.bg,
    bgTertiary: palette.bgDeep,
    border: palette.line,
    borderHover: palette.edgeStrong,
    divider: palette.line,
  }
}

/** 调色盘展开为运行时 CSS 变量；App.vue 在根节点逐项写入。 */
export function paletteToCssVars(palette: ThemePalette): Record<string, string> {
  return {
    '--po-bg': palette.bg,
    '--po-bg-deep': palette.bgDeep,
    '--po-paper': palette.paper,
    '--po-raised': palette.raised,
    '--po-ink': palette.ink,
    '--po-ink-soft': palette.inkSoft,
    '--po-ink-faint': palette.inkFaint,
    '--po-disabled': palette.disabled,
    '--po-line': palette.line,
    '--po-line-mid': palette.lineMid,
    '--po-edge': palette.edge,
    '--po-edge-strong': palette.edgeStrong,
    '--po-primary': palette.primary,
    '--po-primary-hover': palette.primaryHover,
    '--po-primary-pressed': palette.primaryPressed,
    '--po-primary-soft': palette.primarySoft,
    '--po-primary-soft-hover': palette.primarySoftHover,
    '--po-primary-soft-pressed': palette.primarySoftPressed,
    '--po-on-primary': palette.onPrimary,
    '--po-accent': palette.accent,
    '--po-accent-hover': palette.accentHover,
    '--po-accent-pressed': palette.accentPressed,
    '--po-accent-soft': palette.accentSoft,
    '--po-coral': palette.coral,
    '--po-coral-hover': palette.coralHover,
    '--po-coral-deep': palette.coralDeep,
    '--po-on-coral': palette.onCoral,
    '--po-sun': palette.sun,
    '--po-sun-soft': palette.sunSoft,
    '--po-sky': palette.sky,
    '--po-peach': palette.peach,
    '--po-mint': palette.mint,
    '--po-lilac': palette.lilac,
    '--po-gold': palette.sun,
    '--po-gold-soft': palette.sunSoft,
    '--po-good': palette.success,
    '--po-warn': palette.warning,
    '--po-danger': palette.danger,
    '--po-info': palette.info,
    '--po-success-soft': palette.successSoft,
    '--po-warning-soft': palette.warningSoft,
    '--po-danger-soft': palette.dangerSoft,
    '--po-info-soft': palette.infoSoft,
    '--po-dot': palette.dot,
    '--po-scrim': palette.scrim,
  }
}
