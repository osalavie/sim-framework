<script setup lang="ts">
import { computed } from 'vue'
import { BaseEdge, Position, type EdgeProps, type GraphNode, type XYPosition } from '@vue-flow/core'

const props = defineProps<EdgeProps>()

interface EdgeAnchor extends XYPosition {
  position: Position
}

const nodeCenter = (node: GraphNode): XYPosition => ({
  x: node.computedPosition.x + node.dimensions.width / 2,
  y: node.computedPosition.y + node.dimensions.height / 2,
})

/** 连线端点不是固定桩位：节点中心指向对端中心的射线与节点边框的交点，贴近哪条边就落在哪条边上。 */
const borderAnchor = (node: GraphNode, toward: XYPosition): EdgeAnchor => {
  const center = nodeCenter(node)
  const dx = toward.x - center.x
  const dy = toward.y - center.y
  if (!dx && !dy) return { ...center, position: Position.Bottom }

  const scaleX = dx ? node.dimensions.width / 2 / Math.abs(dx) : Number.POSITIVE_INFINITY
  const scaleY = dy ? node.dimensions.height / 2 / Math.abs(dy) : Number.POSITIVE_INFINITY
  const scale = Math.min(scaleX, scaleY)

  return {
    x: center.x + dx * scale,
    y: center.y + dy * scale,
    position:
      scaleX < scaleY
        ? dx > 0
          ? Position.Right
          : Position.Left
        : dy > 0
          ? Position.Bottom
          : Position.Top,
  }
}

const SIDE_NORMALS: Record<Position, XYPosition> = {
  [Position.Top]: { x: 0, y: -1 },
  [Position.Right]: { x: 1, y: 0 },
  [Position.Bottom]: { x: 0, y: 1 },
  [Position.Left]: { x: -1, y: 0 },
}

/** 曲线几何：端点贴边，控制点沿所在边的法线外推，任意相对位置下都保持平滑。 */
const geometry = computed(() => {
  const source = borderAnchor(props.sourceNode, nodeCenter(props.targetNode))
  const target = borderAnchor(props.targetNode, nodeCenter(props.sourceNode))

  const distance = Math.hypot(target.x - source.x, target.y - source.y)
  const bend = Math.min(120, Math.max(36, distance * 0.25))
  const c1x = source.x + SIDE_NORMALS[source.position].x * bend
  const c1y = source.y + SIDE_NORMALS[source.position].y * bend
  const c2x = target.x + SIDE_NORMALS[target.position].x * bend
  const c2y = target.y + SIDE_NORMALS[target.position].y * bend

  return {
    path: `M ${source.x} ${source.y} C ${c1x} ${c1y}, ${c2x} ${c2y}, ${target.x} ${target.y}`,
    labelX: (source.x + 3 * c1x + 3 * c2x + target.x) / 8,
    labelY: (source.y + 3 * c1y + 3 * c2y + target.y) / 8,
  }
})
</script>

<template>
  <BaseEdge
    :id="id"
    :path="geometry.path"
    :label-x="geometry.labelX"
    :label-y="geometry.labelY"
    :label="label"
    :label-style="labelStyle"
    :label-show-bg="labelShowBg"
    :label-bg-style="labelBgStyle"
    :label-bg-padding="labelBgPadding"
    :label-bg-border-radius="labelBgBorderRadius"
    :marker-start="markerStart"
    :marker-end="markerEnd"
    :interaction-width="interactionWidth"
    :style="style"
  />
</template>
