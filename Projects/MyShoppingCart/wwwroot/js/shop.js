/* Shop page: SPA cart interactions (no page refresh). */

document.addEventListener("DOMContentLoaded", () => {
    const badge = document.getElementById("cart-badge");
    const cartButton = document.getElementById("cart-button");
    const drawer = document.getElementById("cart-drawer");
    const overlay = document.getElementById("cart-overlay");
    const drawerContent = document.getElementById("cart-drawer-content");

    function updateBadge(count, animate) {
        badge.textContent = count;
        badge.classList.toggle("invisible", count === 0);
        if (animate && count > 0) {
            badge.classList.remove("animate-badge-pulse");
            void badge.offsetWidth; // restart animation
            badge.classList.add("animate-badge-pulse");
        }
    }

    function updateCardQty(productId, qty) {
        const card = document.querySelector(`.product-card[data-product-id="${productId}"]`);
        if (!card) return;
        card.querySelector(".qty-value").textContent = qty;
        card.querySelector(".qty-minus").disabled = qty === 0;
    }

    async function changeQty(productId, action) {
        try {
            const url = action === "add" ? `/Shop/Add/${productId}` : `/Shop/Remove/${productId}`;
            const response = await fetch(url, { method: "POST" });
            if (!response.ok) {
                console.error(`Cart update failed: ${response.status} ${url}`);
                return;
            }
            const data = await response.json();
            updateBadge(data.count, action === "add");
            updateCardQty(productId, data.itemQty);
            if (!drawer.classList.contains("hidden")) {
                await loadCart();
            }
        } catch (error) {
            console.error("Cart update failed - is the server running?", error);
        }
    }

    // Event delegation: handles + / - on product cards and inside the cart drawer.
    document.addEventListener("click", (event) => {
        const btn = event.target.closest(".qty-btn");
        if (btn) {
            const host = btn.closest("[data-product-id]");
            if (host) changeQty(host.dataset.productId, btn.dataset.action);
            return;
        }
        if (event.target.closest("#cart-close")) {
            closeCart();
        }
    });

    async function loadCart() {
        const response = await fetch("/Shop/Cart");
        drawerContent.innerHTML = await response.text();
    }

    async function openCart() {
        await loadCart();
        drawer.classList.remove("hidden");
        overlay.classList.remove("hidden");
    }

    function closeCart() {
        drawer.classList.add("hidden");
        overlay.classList.add("hidden");
    }

    cartButton.addEventListener("click", openCart);
    overlay.addEventListener("click", closeCart);
    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && !drawer.classList.contains("hidden")) closeCart();
    });
});
