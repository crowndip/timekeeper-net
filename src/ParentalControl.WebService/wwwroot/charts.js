window.chartInstances = {};

window.renderUsageChart = (canvasId, labels, datasets) => {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;

    // Destroy existing chart
    if (window.chartInstances[canvasId]) {
        window.chartInstances[canvasId].destroy();
    }

    // Create new chart
    window.chartInstances[canvasId] = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: datasets.map(ds => ({
                label: ds.label,
                data: ds.data,
                backgroundColor: ds.backgroundColor,
                borderColor: ds.borderColor,
                borderWidth: 1
            }))
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            scales: {
                y: {
                    beginAtZero: true,
                    title: {
                        display: true,
                        text: 'Minutes'
                    }
                },
                x: {
                    title: {
                        display: true,
                        text: 'Date'
                    }
                }
            },
            plugins: {
                legend: {
                    display: true,
                    position: 'top'
                },
                tooltip: {
                    callbacks: {
                        label: function(context) {
                            let label = context.dataset.label || '';
                            if (label) {
                                label += ': ';
                            }
                            label += context.parsed.y + ' min';
                            const hours = (context.parsed.y / 60).toFixed(1);
                            label += ' (' + hours + ' hrs)';
                            return label;
                        }
                    }
                }
            }
        }
    });
};
