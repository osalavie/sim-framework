<template>
  <div class="top-bar-container">
    <div class="icon-bar">
        <div class="app-icon">A</div>
        <div class="app-icon">B</div>
        <div class="app-icon">C</div>
        <div class="app-icon">D</div>
    </div>
  </div>
</template>
<style>
        /* 容器Bar的样式 */
        .icon-bar {
            display: flex;
            justify-content: space-around;
            align-items: center;
            width: 100%;
            max-width: 400px;
            height: 60px;
            background-color: var(--theme-bg-tertiary);
            border-radius: 12px;
            box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
            position: relative; /* 为子元素的绝对定位提供参考 */
            margin: 50px auto;
        }

        /* 应用图标的基础样式 */
        .app-icon {
            width: 40px;
            height: 40px;
            background-color: #4a6cf7;
            border-radius: 8px;
            display: flex;
            align-items: center;
            justify-content: center;
            color: white;
            font-weight: bold;
            cursor: pointer;
            transition: all 0.3s ease; /* 平滑过渡效果 */
            position: relative; /* 为可能的绝对定位子元素做准备 */
        }

        /* 鼠标悬停时的关键效果 */
        .app-icon:hover {
            transform: translateY(-20px) scale(1.1); /* 向上移动并略微放大 */
            z-index: 10; /* 确保图标浮于Bar之上 */
            box-shadow: 0 5px 15px rgba(74, 108, 247, 0.4); /* 添加阴影增强悬浮感 */
        }
    </style>
