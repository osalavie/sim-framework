import { h, type Component } from 'vue'
import { NIcon } from 'naive-ui'
import { LoadingOutlined } from '@vicons/antd'
import {
  Api,
  Application,
  ArrowLeft,
  Asleep,
  Calendar,
  ChartLineData,
  Chat,
  CheckmarkOutline,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Close,
  DataShare,
  Document,
  Download,
  Edit,
  Earth,
  FileStorage,
  Flow,
  FlowData,
  Grid,
  GroupSecurity,
  Home,
  Idea,
  Information,
  Light,
  List,
  ListBoxes,
  Locked,
  Menu,
  Number1,
  PenFountain,
  Redo,
  Save,
  Search,
  SearchLocate,
  Security,
  SendAlt,
  SendToBack,
  Settings,
  SettingsCheck,
  Share,
  Task,
  ThumbsUp,
  Time,
  TreeView,
  TwoFactorAuthentication,
  Undo,
  Unlocked,
  User,
  UserRole,
  View,
  WarningAlt,
  WarningSquare,
} from '@vicons/carbon'
import {
  ChatbubbleOutline,
  CodeOutline,
  DocumentOutline,
  DocumentTextOutline,
  FlaskOutline,
  FolderOutline,
  GitNetworkOutline,
  GridOutline,
  HomeOutline,
  KeyOutline,
  ListOutline,
  OpenOutline,
  PeopleOutline,
  PencilOutline,
  PersonCircleOutline,
  PersonOutline,
  PricetagOutline,
  PricetagsOutline,
  ShieldCheckmarkOutline,
  BusinessOutline,
} from '@vicons/ionicons5'

type IconLibrary = 'antd' | 'carbon' | 'ionicons5' | 'mdi'

// Keep this registry explicit. Namespace imports make every icon part of the initial bundle.
const iconLibraries: Record<IconLibrary, Readonly<Record<string, Component>>> = {
  antd: { LoadingOutlined },
  carbon: {
    Api,
    Application,
    ArrowLeft,
    Asleep,
    Calendar,
    ChartLineData,
    Chat,
    CheckmarkOutline,
    ChevronDown,
    ChevronLeft,
    ChevronRight,
    Close,
    DataShare,
    Document,
    Download,
    Edit,
    Earth,
    FileStorage,
    Flow,
    FlowData,
    Grid,
    GroupSecurity,
    Home,
    Idea,
    Information,
    Light,
    List,
    ListBoxes,
    Locked,
    Menu,
    Number1,
    PenFountain,
    Redo,
    Save,
    Search,
    SearchLocate,
    Security,
    SendAlt,
    SendToBack,
    Settings,
    SettingsCheck,
    Share,
    Task,
    ThumbsUp,
    Time,
    TreeView,
    TwoFactorAuthentication,
    Undo,
    Unlocked,
    User,
    UserRole,
    View,
    WarningAlt,
    WarningSquare,
    // Lowercase / kebab-case aliases for backward compatibility with legacy seed data
    'cloud-storage': FileStorage,
    api: Api,
    time: Time,
    settings: Settings,
    security: Security,
    notification: Information,
    'number-1': Number1,
    'settings-check': SettingsCheck,
    'data-share': DataShare,
    'list-boxes': ListBoxes,
    // Aliases for icons that don't have direct Carbon equivalents in the bundle
    Dashboard: Application,
    Logout: ArrowLeft,
    Notification: Information,
  },
  ionicons5: {
    ChatbubbleOutline,
    CodeOutline,
    DocumentOutline,
    DocumentTextOutline,
    FlaskOutline,
    FolderOutline,
    GitNetworkOutline,
    GridOutline,
    HomeOutline,
    KeyOutline,
    ListOutline,
    PeopleOutline,
    BusinessOutline,
    PricetagOutline,
    ShieldCheckmarkOutline,
    dashboard: GridOutline,
    list: ListOutline,
  },
  // Legacy seed data used Iconify MDI names before the loader became explicit.
  // Map only those known names to equivalent bundled icons to keep existing databases compatible.
  mdi: {
    'account-circle-outline': PersonCircleOutline,
    'account-edit-outline': PersonCircleOutline,
    'account-outline': PersonOutline,
    'cog-outline': Settings,
    'comment-text-multiple-outline': ChatbubbleOutline,
    'file-document-multiple-outline': DocumentTextOutline,
    'folder-outline': FolderOutline,
    'open-in-new': OpenOutline,
    'pencil-outline': PencilOutline,
    'post-outline': DocumentTextOutline,
    'tag-outline': PricetagsOutline,
  },
}

function parseIconName(iconName: string): { library: string; name: string } {
  const separatorIndex = iconName.indexOf(':')
  if (separatorIndex < 0) return { library: 'ionicons5', name: iconName }

  return {
    library: iconName.slice(0, separatorIndex),
    name: iconName.slice(separatorIndex + 1),
  }
}

function warningIcon(title: string): Component {
  return {
    setup() {
      return () => h(WarningSquare, { title })
    },
  }
}

export function createIconComponent(iconName: string): Component {
  if (!iconName) return warningIcon('图标名称为空')

  const { library, name } = parseIconName(iconName)
  const iconLibrary = iconLibraries[library as IconLibrary]
  if (!iconLibrary) {
    console.warn(`Unknown icon library: ${library}`)
    return warningIcon('图标库不受支持')
  }

  const iconComponent = iconLibrary[name]
  if (iconComponent) return iconComponent

  console.warn(`Icon not registered: ${iconName}`)
  return warningIcon('图标未注册')
}

export function createIconRender(iconName: string, size?: number | string) {
  return () => {
    const IconComponent = createIconComponent(iconName)
    return h(NIcon, { size }, { default: () => h(IconComponent) })
  }
}
