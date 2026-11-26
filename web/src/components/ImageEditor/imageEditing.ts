export interface CropPercent {
  x: number
  y: number
  width: number
  height: number
}

export interface ImageEditOptions {
  crop: CropPercent
  maxWidth?: number
  maxHeight?: number
  quality: number
  format: 'image/jpeg' | 'image/png' | 'image/webp'
}

export interface NormalizeCropOptions {
  /**
   * When set, the returned crop will be reshaped to the given aspect ratio
   * (width / height) in **pixel space** while remaining inside the source
   * bounds. `1` means a square (1:1). Requires `imageWidth` / `imageHeight`
   * to be meaningful; without them the ratio is applied in percentage space
   * (which is only correct for square source images).
   */
  aspectRatio?: number
  imageWidth?: number
  imageHeight?: number
}

/**
 * Normalize a percentage-based crop to stay within [0, 100] bounds. When an
 * `aspectRatio` is provided together with the source image dimensions the crop
 * is reshaped in **pixel space** so that the locked ratio is truly correct even
 * for non-square source images.
 */
export function normalizeCrop(crop: CropPercent, options: NormalizeCropOptions = {}): CropPercent {
  const { aspectRatio, imageWidth, imageHeight } = options
  const x = clamp(crop.x, 0, 99)
  const y = clamp(crop.y, 0, 99)
  const width = clamp(crop.width, 1, 100 - x)
  const height = clamp(crop.height, 1, 100 - y)

  if (aspectRatio && aspectRatio > 0 && imageWidth && imageHeight) {
    // Work in pixel space so the ratio is correct for non-square images.
    const pxX = (imageWidth * x) / 100
    const pxY = (imageHeight * y) / 100
    const maxPxW = imageWidth - pxX
    const maxPxH = imageHeight - pxY

    // Try width-driven first.
    let pxW = (imageWidth * width) / 100
    let pxH = pxW / aspectRatio
    if (pxH > maxPxH) {
      pxH = maxPxH
      pxW = pxH * aspectRatio
    }
    if (pxW > maxPxW) {
      pxW = maxPxW
      pxH = pxW / aspectRatio
    }

    return {
      x: (pxX / imageWidth) * 100,
      y: (pxY / imageHeight) * 100,
      width: (pxW / imageWidth) * 100,
      height: (pxH / imageHeight) * 100,
    }
  }

  return { x, y, width, height }
}

/**
 * Derive the largest possible crop (in percentage) with the given aspect ratio
 * that fits inside the source image. The crop is centered. When `imageWidth`
 * and `imageHeight` are supplied the ratio is applied in pixel space (correct
 * for non-square images); otherwise it falls back to percentage space.
 */
export function createCenteredCrop(
  aspectRatio: number,
  imageWidth: number = 0,
  imageHeight: number = 0,
): CropPercent {
  if (!aspectRatio || aspectRatio <= 0) {
    return { x: 0, y: 0, width: 100, height: 100 }
  }

  if (imageWidth && imageHeight) {
    let pxW: number
    let pxH: number
    if (imageWidth / imageHeight > aspectRatio) {
      pxH = imageHeight
      pxW = pxH * aspectRatio
    } else {
      pxW = imageWidth
      pxH = pxW / aspectRatio
    }
    const pxX = (imageWidth - pxW) / 2
    const pxY = (imageHeight - pxH) / 2
    return {
      x: (pxX / imageWidth) * 100,
      y: (pxY / imageHeight) * 100,
      width: (pxW / imageWidth) * 100,
      height: (pxH / imageHeight) * 100,
    }
  }

  // Fallback: percentage space (only correct for square images).
  if (aspectRatio >= 1) {
    const height = 100 / aspectRatio
    return { x: 0, y: (100 - height) / 2, width: 100, height }
  }
  const width = 100 * aspectRatio
  return { x: (100 - width) / 2, y: 0, width, height: 100 }
}

export function calculatePixelCrop(imageWidth: number, imageHeight: number, crop: CropPercent) {
  const normalized = normalizeCrop(crop)
  return {
    x: Math.floor((imageWidth * normalized.x) / 100),
    y: Math.floor((imageHeight * normalized.y) / 100),
    width: Math.max(1, Math.floor((imageWidth * normalized.width) / 100)),
    height: Math.max(1, Math.floor((imageHeight * normalized.height) / 100)),
  }
}

export function calculateOutputSize(
  width: number,
  height: number,
  maxWidth?: number,
  maxHeight?: number,
) {
  const widthScale = maxWidth && maxWidth > 0 ? maxWidth / width : 1
  const heightScale = maxHeight && maxHeight > 0 ? maxHeight / height : 1
  const scale = Math.min(1, widthScale, heightScale)
  return {
    width: Math.max(1, Math.round(width * scale)),
    height: Math.max(1, Math.round(height * scale)),
  }
}

export async function editImage(file: File, options: ImageEditOptions): Promise<File> {
  const image = await decodeImage(file)
  try {
    const crop = calculatePixelCrop(image.width, image.height, options.crop)
    const output = calculateOutputSize(crop.width, crop.height, options.maxWidth, options.maxHeight)
    const canvas = document.createElement('canvas')
    canvas.width = output.width
    canvas.height = output.height
    const context = canvas.getContext('2d')
    if (!context) throw new Error('Canvas 2D context is unavailable.')
    context.imageSmoothingEnabled = true
    context.imageSmoothingQuality = 'high'
    context.drawImage(
      image.source,
      crop.x,
      crop.y,
      crop.width,
      crop.height,
      0,
      0,
      output.width,
      output.height,
    )

    const blob = await new Promise<Blob>((resolve, reject) => {
      canvas.toBlob(
        (value) => (value ? resolve(value) : reject(new Error('Image encoding failed.'))),
        options.format,
        clamp(options.quality, 1, 100) / 100,
      )
    })
    return new File([blob], replaceExtension(file.name, extensionFor(options.format)), {
      type: options.format,
      lastModified: Date.now(),
    })
  } finally {
    image.close()
  }
}

export function clamp(value: number, minimum: number, maximum: number) {
  return Math.min(maximum, Math.max(minimum, Number.isFinite(value) ? value : minimum))
}

function replaceExtension(fileName: string, extension: string) {
  const baseName = fileName.replace(/\.[^.]+$/, '') || 'image'
  return `${baseName}-edited.${extension}`
}

function extensionFor(format: ImageEditOptions['format']) {
  if (format === 'image/png') return 'png'
  if (format === 'image/webp') return 'webp'
  return 'jpg'
}

async function decodeImage(file: File): Promise<{
  source: CanvasImageSource
  width: number
  height: number
  close: () => void
}> {
  if ('createImageBitmap' in window) {
    const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' })
    return {
      source: bitmap,
      width: bitmap.width,
      height: bitmap.height,
      close: () => bitmap.close(),
    }
  }

  const url = URL.createObjectURL(file)
  const image = new Image()
  image.decoding = 'async'
  image.src = url
  try {
    await image.decode()
    return {
      source: image,
      width: image.naturalWidth,
      height: image.naturalHeight,
      close: () => URL.revokeObjectURL(url),
    }
  } catch (error) {
    URL.revokeObjectURL(url)
    throw error
  }
}
