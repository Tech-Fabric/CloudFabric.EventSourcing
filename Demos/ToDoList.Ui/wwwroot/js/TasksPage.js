(function () {
    class ToDoList {
        constructor(rootContainer, draggableSelector, hoverCallbackObject, horizontal = false) {
            this.containerQuery = document.querySelector(rootContainer);
            this.tasks = this.containerQuery.querySelectorAll(draggableSelector);
            
            this.draggableSelector = draggableSelector;
            
            this.horizontal = horizontal;
            
            this.draggable = null;
            this.dragLeaveQueue = [];
            this.animationInProgress = false;
            this.lastCapturedDragOverEvent = null;
            this.lastCapturedDragOverEventTarget = null;

            this.taskDragAnimationDuration = 0;

            this.hoverCallbackObject = hoverCallbackObject;
            
            this.lastDraggedOverElement = null;
            this.lastDraggedOverAbove = false;
            
            if (this.tasks.length > 0) {
                this.taskDragAnimationDuration = parseFloat(window.getComputedStyle(this.tasks[0]).transitionDuration) * 1000;
            }

            this.cleanDragOverAnimationsBound = this.cleanDragOverAnimations.bind(this);
            this.onAfterAnimationCompletesBound = this.onAfterAnimationCompletes.bind(this);
            
            this.tasks.forEach((taskContainer) => {
                taskContainer.addEventListener("dragstart", (e) => {
                    if(!e.target.matches(this.draggableSelector)) {
                        return;
                    }
                    
                    this.draggable = e.target;
                    
                    this.draggable.classList.add("dragging");

                    this.tasks.forEach((taskContentElement) => {
                        taskContentElement.classList.add("no-pointer-events-on-children");
                    });
                    
                    console.log("dragstart", e.target);
                });

                taskContainer.addEventListener("dragend", (e) => {
                    if(!e.target.matches(this.draggableSelector)) {
                        return;
                    }
                    
                    console.log("dragend");
                    
                    this.draggable = e.target;
                    
                    this.draggable.classList.remove("dragging");
                    var draggablePreviousSibling = this.draggable.previousSibling;

                    let previousContainerScrollTopBeforeTaskRemove = this.draggable.parentElement.scrollTop;
                    
                    if(this.lastDraggedOverAbove) {
                        this.lastDraggedOverElement.parentElement.insertBefore(this.draggable, this.lastDraggedOverElement);
                    } else {
                        this.lastDraggedOverElement.parentElement.insertBefore(this.draggable, this.lastDraggedOverElement.nextSibling);
                    }
                    
                    if(draggablePreviousSibling != null) {
                        draggablePreviousSibling.classList.add("no-animation");

                        if (this.horizontal) {
                            draggablePreviousSibling.style.paddingRight = (this.draggable.getBoundingClientRect().width + 10) + "px";
                        } else {
                            draggablePreviousSibling.style.paddingBottom = (this.draggable.getBoundingClientRect().height + 10) + "px";
                        }
                        draggablePreviousSibling.offsetHeight;
                        draggablePreviousSibling.classList.remove("no-animation");
                        draggablePreviousSibling.parentElement.scrollTop = previousContainerScrollTopBeforeTaskRemove;
                    }

                    this.lastDraggedOverElement.classList.add("no-animation");
                    this.cleanDragOverAnimationsBound(this.lastDraggedOverElement);
                    this.lastDraggedOverElement.offsetHeight;
                    this.lastDraggedOverElement.classList.remove("no-animation");

                    this.lastCapturedDragOverEvent = null;
                    this.lastCapturedDragOverEventTarget = null;

                    setTimeout(() => {
                        this.tasks.forEach(this.cleanDragOverAnimationsBound);

                        this.tasks.forEach((taskContentElement) => {
                            taskContentElement.classList.remove("no-pointer-events-on-children");
                        });
                    }, 200);
                });

                taskContainer.addEventListener("dragover", this.dragOverEventHandler.bind(this), true);

                taskContainer.addEventListener("dragleave", (e) => {
                    if(!e.target.matches(this.draggableSelector)) {
                        return;
                    }
                    
                    let target = e.target;
                    
                    this.dragLeaveQueue.push(target);

                    setTimeout(() => this.cleanDragOverAnimationsBound(target), this.taskDragAnimationDuration + 10);
                });
            });
        }
        
        dragOverEventHandler(e) {
            if(!e.target.matches(this.draggableSelector)) {
                return;
            }
            
            e.preventDefault();

            let target = e.target;
            
            if (target === this.draggable) {
                return false;
            }

            if (this.animationInProgress) {
                this.lastCapturedDragOverEvent = new e.constructor(e.type, e);
                this.lastCapturedDragOverEventTarget = e.target;
                return;
            }

            while (this.dragLeaveQueue.length > 0) {
                this.cleanDragOverAnimations(this.dragLeaveQueue.pop());
            }

            if(this.horizontal){
                let sourceWidth = this.draggable.getBoundingClientRect().width;
                let targetWidth = target.getBoundingClientRect().width;

                var left = e.offsetX < (targetWidth / 2);

                if (left) {
                    if (!target.classList.contains("dragover-left")) {
                        target.classList.remove("dragover-right");
                        target.classList.add("dragover-left");
                    }

                    target.style.paddingRight = "10px";
                    target.style.paddingLeft = (sourceWidth + 10) + "px";
                    console.log("left");
                } else {
                    if (!target.classList.contains("dragover-right")) {
                        target.classList.remove("dragover-left");
                        target.classList.add("dragover-right");
                    }

                    target.style.paddingLeft = "10px";
                    target.style.paddingRight = (sourceWidth + 10) + "px";
                    console.log("right");
                }
            } else {
                let sourceHeight = this.draggable.getBoundingClientRect().height;
                let targetHeight = target.getBoundingClientRect().height;

                var above = targetHeight / 2 > e.offsetY;

                if (above) {
                    if (!target.classList.contains("dragover-above")) {
                        target.classList.remove("dragover-below");
                        target.classList.add("dragover-above");
                    }

                    target.style.paddingBottom = "10px";
                    target.style.paddingTop = sourceHeight + "px";
                } else {
                    if (!target.classList.contains("dragover-below")) {
                        target.classList.remove("dragover-above");
                        target.classList.add("dragover-below");
                    }

                    target.style.paddingTop = "0px";
                    target.style.paddingBottom = (sourceHeight + 10) + "px";
                }
            }

            this.animationInProgress = true;
            setTimeout(this.onAfterAnimationCompletesBound, this.taskDragAnimationDuration);
            this.hoverCallbackObject.invokeMethodAsync("UpdateHoveredTaskInformation", e.target.dataset["hover"], left);
            
            this.lastDraggedOverElement = e.target;
            this.lastDraggedOverAbove = left;
        };

        cleanDragOverAnimations(element) {
            if(this.horizontal) {
                element.style.paddingLeft = "10px";
                element.style.paddingRight = "10px";

                element.classList.remove("dragover-left");
                element.classList.remove("dragover-right");
            } else {
                element.style.paddingTop = "0px";
                element.style.paddingBottom = "10px";

                element.classList.remove("dragover-above");
                element.classList.remove("dragover-below");
            }
        }

        onAfterAnimationCompletes() {
            this.animationInProgress = false;

            if (this.lastCapturedDragOverEvent != null) {
                this.lastCapturedDragOverEventTarget.dispatchEvent(this.lastCapturedDragOverEvent);
                this.lastCapturedDragOverEvent = null;
            }
        }
    }

    window.ToDoList = {
        initializeTaskDragAndDrop: function (container, draggableSelector, hoverCallbackObject, horizontal) {
            new ToDoList(container, draggableSelector, hoverCallbackObject, horizontal);
        }
    };
})();