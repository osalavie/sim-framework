import { BRAND_NAME } from '@/config/branding'

export type ProviderFieldType = 'text' | 'password' | 'select' | 'textarea'

export interface ProviderFieldOption {
  labelKey: string
  value: string
}

export interface ProviderField {
  key: string
  labelKey: string
  hintKey?: string
  type?: ProviderFieldType
  secret?: boolean
  required?: boolean
  defaultValue?: string
  options?: ProviderFieldOption[]
}

export interface ProviderFieldGroup {
  titleKey: string
  descriptionKey?: string
  fields: ProviderField[]
}

export interface ProviderSchema {
  channel: string
  labelKey: string
  summaryKey: string
  groups: ProviderFieldGroup[]
}

const endpoint = (
  secret = false,
  required = true,
  defaultValue = '',
): ProviderField => ({
  key: 'Endpoint',
  labelKey: secret ? 'Notifications:Field:WebhookUrl' : 'Notifications:Field:Endpoint',
  hintKey: secret
    ? 'Notifications:Field:WebhookUrlHint'
    : 'Notifications:Field:EndpointHint',
  type: secret ? 'password' : 'text',
  secret,
  required,
  defaultValue,
})

const tencentCredentials: ProviderField[] = [
  {
    key: 'SecretId',
    labelKey: 'Notifications:Field:SecretId',
    type: 'password',
    secret: true,
  },
  {
    key: 'SecretKey',
    labelKey: 'Notifications:Field:SecretKey',
    type: 'password',
    secret: true,
  },
]

const aliyunCredentials: ProviderField[] = [
  {
    key: 'AccessKeyId',
    labelKey: 'Notifications:Field:AccessKeyId',
  },
  {
    key: 'AccessKeySecret',
    labelKey: 'Notifications:Field:AccessKeySecret',
    type: 'password',
    secret: true,
  },
]

const regionOptions: ProviderFieldOption[] = [
  { labelKey: 'Notifications:Region:Guangzhou', value: 'ap-guangzhou' },
  { labelKey: 'Notifications:Region:Shanghai', value: 'ap-shanghai' },
  { labelKey: 'Notifications:Region:Beijing', value: 'ap-beijing' },
  { labelKey: 'Notifications:Region:Nanjing', value: 'ap-nanjing' },
]

const tencentConnection = (defaultEndpoint: string): ProviderFieldGroup => ({
  titleKey: 'Notifications:Group:Connection',
  descriptionKey: 'Notifications:Group:TencentConnectionHint',
  fields: [
    endpoint(false, true, defaultEndpoint),
    {
      key: 'Region',
      labelKey: 'Field:Region',
      type: 'select',
      defaultValue: 'ap-guangzhou',
      options: regionOptions,
    },
  ],
})

const standardHttpGroups = (summaryKey: string): ProviderSchema => ({
  channel: summaryKey.includes('voice') ? 'voice' : 'sms',
  labelKey: summaryKey.replace('.summary.', '.provider.'),
  summaryKey,
  groups: [
    {
      titleKey: 'Notifications:Group:Connection',
      descriptionKey: 'Notifications:Group:StandardHttpHint',
      fields: [endpoint()],
    },
    {
      titleKey: 'Notifications:Group:Credentials',
      descriptionKey: 'Notifications:Group:OptionalCredentialsHint',
      fields: [
        {
          key: 'BearerToken',
          labelKey: 'Notifications:Field:BearerToken',
          type: 'password',
          secret: true,
        },
        {
          key: 'SigningSecret',
          labelKey: 'Notifications:Field:SigningSecret',
          type: 'password',
          secret: true,
        },
      ],
    },
  ],
})

export const providerSchemas: Record<string, ProviderSchema> = {
  inbox: {
    channel: 'inbox',
    labelKey: 'Notifications:Provider:Inbox',
    summaryKey: 'Notifications:Summary:Inbox',
    groups: [],
  },
  browser_web_push: {
    channel: 'browser',
    labelKey: 'Notifications:Provider:Browser',
    summaryKey: 'Notifications:Summary:Browser',
    groups: [],
  },
  smtp: {
    channel: 'email',
    labelKey: 'Notifications:Provider:Smtp',
    summaryKey: 'Notifications:Summary:Smtp',
    groups: [
      {
        titleKey: 'Notifications:Group:Connection',
        descriptionKey: 'Notifications:Group:SmtpConnectionHint',
        fields: [
          {
            key: 'Host',
            labelKey: 'Notifications:Field:Host',
            required: true,
          },
          {
            key: 'Port',
            labelKey: 'Notifications:Field:Port',
            required: true,
            defaultValue: '587',
          },
          {
            key: 'SocketOptions',
            labelKey: 'Notifications:Field:TlsMode',
            type: 'select',
            defaultValue: 'StartTlsWhenAvailable',
            options: [
              {
                labelKey: 'Notifications:Tls:StartTlsWhenAvailable',
                value: 'StartTlsWhenAvailable',
              },
              { labelKey: 'Notifications:Tls:StartTls', value: 'StartTls' },
              { labelKey: 'Notifications:Tls:SslOnConnect', value: 'SslOnConnect' },
              { labelKey: 'Notifications:Tls:None', value: 'None' },
            ],
          },
        ],
      },
      {
        titleKey: 'Notifications:Group:Sender',
        fields: [
          {
            key: 'FromAddress',
            labelKey: 'Notifications:Field:FromAddress',
            required: true,
          },
          {
            key: 'FromName',
            labelKey: 'Notifications:Field:FromName',
            defaultValue: BRAND_NAME,
          },
          {
            key: 'IsBodyHtml',
            labelKey: 'Notifications:Field:BodyFormat',
            type: 'select',
            defaultValue: 'false',
            options: [
              { labelKey: 'Common:PlainText', value: 'false' },
              { labelKey: 'Notifications:Format:Html', value: 'true' },
            ],
          },
        ],
      },
      {
        titleKey: 'Notifications:Group:Credentials',
        descriptionKey: 'Notifications:Group:SmtpCredentialsHint',
        fields: [
          { key: 'UserName', labelKey: 'Field:Username' },
          {
            key: 'Password',
            labelKey: 'Field:Password',
            type: 'password',
            secret: true,
          },
        ],
      },
    ],
  },
  tencent_sms: {
    channel: 'sms',
    labelKey: 'Notifications:Provider:TencentSms',
    summaryKey: 'Notifications:Summary:TencentSms',
    groups: [
      tencentConnection('https://sms.tencentcloudapi.com'),
      {
        titleKey: 'Notifications:Group:Credentials',
        fields: tencentCredentials,
      },
      {
        titleKey: 'Notifications:Group:Template',
        descriptionKey: 'Notifications:Group:TemplateHint',
        fields: [
          {
            key: 'SmsSdkAppId',
            labelKey: 'Notifications:Field:SmsSdkAppId',
            required: true,
          },
          { key: 'SignName', labelKey: 'Notifications:Field:SignName', required: true },
          { key: 'TemplateId', labelKey: 'Notifications:Field:TemplateId' },
          {
            key: 'TemplateParameterOrder',
            labelKey: 'Notifications:Field:TemplateParameterOrder',
            hintKey: 'Notifications:Field:TemplateParameterOrderHint',
          },
        ],
      },
    ],
  },
  aliyun_sms: {
    channel: 'sms',
    labelKey: 'Notifications:Provider:AliyunSms',
    summaryKey: 'Notifications:Summary:AliyunSms',
    groups: [
      {
        titleKey: 'Notifications:Group:Connection',
        fields: [endpoint(false, true, 'https://dysmsapi.aliyuncs.com')],
      },
      {
        titleKey: 'Notifications:Group:Credentials',
        fields: aliyunCredentials,
      },
      {
        titleKey: 'Notifications:Group:Template',
        fields: [
          { key: 'SignName', labelKey: 'Notifications:Field:SignName', required: true },
          { key: 'TemplateCode', labelKey: 'Notifications:Field:TemplateCode' },
        ],
      },
    ],
  },
  standard_http_sms: standardHttpGroups('Notifications:Summary:StandardSms'),
  generic_webhook: {
    channel: 'webhook',
    labelKey: 'Notifications:Provider:GenericWebhook',
    summaryKey: 'Notifications:Summary:GenericWebhook',
    groups: [
      {
        titleKey: 'Notifications:Group:Connection',
        fields: [endpoint(false, false)],
      },
      {
        titleKey: 'Notifications:Group:Credentials',
        descriptionKey: 'Notifications:Group:OptionalCredentialsHint',
        fields: [
          {
            key: 'BearerToken',
            labelKey: 'Notifications:Field:BearerToken',
            type: 'password',
            secret: true,
          },
          {
            key: 'SigningSecret',
            labelKey: 'Notifications:Field:SigningSecret',
            type: 'password',
            secret: true,
          },
        ],
      },
    ],
  },
  feishu_robot: {
    channel: 'feishu',
    labelKey: 'Notifications:Provider:Feishu',
    summaryKey: 'Notifications:Summary:Feishu',
    groups: [
      {
        titleKey: 'Notifications:Group:Connection',
        descriptionKey: 'Notifications:Group:RobotUrlHint',
        fields: [endpoint(true)],
      },
    ],
  },
  wecom_robot: {
    channel: 'wecom',
    labelKey: 'Notifications:Provider:Wecom',
    summaryKey: 'Notifications:Summary:Wecom',
    groups: [
      {
        titleKey: 'Notifications:Group:Connection',
        descriptionKey: 'Notifications:Group:RobotUrlHint',
        fields: [endpoint(true)],
      },
    ],
  },
  dingtalk_robot: {
    channel: 'dingtalk',
    labelKey: 'Notifications:Provider:Dingtalk',
    summaryKey: 'Notifications:Summary:Dingtalk',
    groups: [
      {
        titleKey: 'Notifications:Group:Connection',
        descriptionKey: 'Notifications:Group:RobotUrlHint',
        fields: [endpoint(true)],
      },
    ],
  },
  tencent_voice: {
    channel: 'voice',
    labelKey: 'Notifications:Provider:TencentVoice',
    summaryKey: 'Notifications:Summary:TencentVoice',
    groups: [
      tencentConnection('https://vms.tencentcloudapi.com'),
      {
        titleKey: 'Notifications:Group:Credentials',
        fields: tencentCredentials,
      },
      {
        titleKey: 'Notifications:Group:Template',
        fields: [
          {
            key: 'VoiceSdkAppid',
            labelKey: 'Notifications:Field:VoiceSdkAppId',
            required: true,
          },
          { key: 'TemplateId', labelKey: 'Notifications:Field:TemplateId' },
          {
            key: 'TemplateParameterOrder',
            labelKey: 'Notifications:Field:TemplateParameterOrder',
            hintKey: 'Notifications:Field:TemplateParameterOrderHint',
          },
          {
            key: 'PlayTimes',
            labelKey: 'Notifications:Field:PlayTimes',
            defaultValue: '1',
          },
        ],
      },
    ],
  },
  aliyun_voice: {
    channel: 'voice',
    labelKey: 'Notifications:Provider:AliyunVoice',
    summaryKey: 'Notifications:Summary:AliyunVoice',
    groups: [
      {
        titleKey: 'Notifications:Group:Connection',
        fields: [endpoint(false, true, 'https://dyvmsapi.aliyuncs.com')],
      },
      {
        titleKey: 'Notifications:Group:Credentials',
        fields: aliyunCredentials,
      },
      {
        titleKey: 'Notifications:Group:Template',
        fields: [
          {
            key: 'CalledShowNumber',
            labelKey: 'Notifications:Field:CalledShowNumber',
          },
          { key: 'TtsCode', labelKey: 'Notifications:Field:TtsCode' },
          {
            key: 'PlayTimes',
            labelKey: 'Notifications:Field:PlayTimes',
            defaultValue: '1',
          },
        ],
      },
    ],
  },
  standard_http_voice: standardHttpGroups('Notifications:Summary:StandardVoice'),
}

export const providerNames = Object.keys(providerSchemas)

export const createProviderSettings = (
  provider: string,
  current: Record<string, string> = {},
) => {
  const values: Record<string, string> = {}
  for (const group of providerSchemas[provider]?.groups ?? []) {
    for (const field of group.fields) {
      if (!field.secret) {
        values[field.key] = current[field.key] ?? field.defaultValue ?? ''
      }
    }
  }
  return values
}

export const getProviderFields = (provider: string) =>
  (providerSchemas[provider]?.groups ?? []).flatMap((group) => group.fields)
